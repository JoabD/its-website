using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Identity.CreateUser;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.CreateStudent;

/// <summary>
/// Alta MANUAL de un alumno (plan de control escolar → Alumnos → "Agregar alumno" → formulario):
/// a diferencia de aprobar una solicitud pública (que siempre entra Cuatrimestral, cuatrimestre 1,
/// RN de producto), aquí el administrador captura los datos directamente y decide plan
/// (Cuatrimestral/Semestral) y en qué cuatrimestre/semestre entra — pensado sobre todo para alumnos
/// que ya venían cursando fuera del sistema. RN-12: solo Administrator.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record CreateStudentCommand(
    string FullName, string Email, string Phone, DateOnly BirthDate, string RegionId,
    Modality Modality, StudyPlan Plan, int CurrentTerm) : ICommand<CreateStudentResponse>;

public sealed record CreateStudentResponse(string UserId, int EnrollmentNumber, string Matricula, string TemporaryPassword);

public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.RegionId).NotEmpty();
        RuleFor(x => x.CurrentTerm).InclusiveBetween(1, 6);
    }
}

public sealed class CreateStudentCommandHandler(
    IUserRepository users, Domain.Catalog.IRegionRepository regions, IEnrollmentNumberGenerator enrollmentNumbers,
    IMatriculaGenerator matriculaGenerator, IPasswordHasher passwordHasher, IEmailSender emailSender, IClock clock)
    : ICommandHandler<CreateStudentCommand, CreateStudentResponse>
{
    public async Task<Result<CreateStudentResponse>> HandleAsync(CreateStudentCommand command, CancellationToken ct)
    {
        var nameResult = PersonName.Create(command.FullName);
        var emailResult = Email.Create(command.Email);
        var phoneResult = PhoneNumber.Create(command.Phone);
        var termResult = TermNumber.Create(command.CurrentTerm);

        if (nameResult.IsFailure) return Result.Failure<CreateStudentResponse>(nameResult.Error);
        if (emailResult.IsFailure) return Result.Failure<CreateStudentResponse>(emailResult.Error);
        if (phoneResult.IsFailure) return Result.Failure<CreateStudentResponse>(phoneResult.Error);
        if (termResult.IsFailure) return Result.Failure<CreateStudentResponse>(termResult.Error);

        var existing = await users.GetByEmailAsync(emailResult.Value.Value, ct);
        if (existing is not null)
        {
            return Result.Failure<CreateStudentResponse>(Error.Conflict("CreateStudent.EmailInUse", "Ya existe un usuario con ese correo."));
        }

        var regionEntity = await regions.GetByIdAsync(command.RegionId, ct);
        if (regionEntity is null)
        {
            return Result.Failure<CreateStudentResponse>(Error.Validation("CreateStudent.InvalidRegion", "La región indicada no existe."));
        }

        var region = new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name);

        // Mismo respaldo que CreateUser.cs (alta de staff): los campos del expediente de admisión
        // (domicilio, iglesia, escolaridad) no se piden en el alta manual/rápida — se guardan como
        // "N/D" y el propio alumno los puede completar después desde "Mi perfil" si aplica.
        var address = Address.Create("N/D", "N/D", "N/D", "N/D").Value;
        var church = ChurchInfo.Create("N/D", address, "N/D", "N/D", MinistryRole.None).Value;
        var education = EducationLevel.Create(SchoolingLevel.Other, "N/D").Value;

        var profileResult = PersonalProfile.Create(
            nameResult.Value, emailResult.Value, phoneResult.Value, command.BirthDate, null, address, church, education, null, null);
        if (profileResult.IsFailure) return Result.Failure<CreateStudentResponse>(profileResult.Error);

        var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
        var matricula = await matriculaGenerator.NextAsync(regionEntity.Abbreviation, ct);
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var userResult = User.CreateStudentManually(
            EntityId.NewId(), enrollmentNumber, matricula, profileResult.Value, command.Modality, region,
            command.Plan, termResult.Value, passwordHasher.Hash(temporaryPassword), clock);
        if (userResult.IsFailure) return Result.Failure<CreateStudentResponse>(userResult.Error);

        await users.AddAsync(userResult.Value, ct);

        await emailSender.SendAsync(
            emailResult.Value.Value,
            "Tus credenciales de acceso — Instituto Teológico Shekinah",
            $"<p>Matrícula: {matricula}</p><p>Contraseña temporal: {temporaryPassword}</p><p>Deberás cambiarla en tu primer inicio de sesión.</p>",
            ct);

        return Result.Success(new CreateStudentResponse(userResult.Value.Id, enrollmentNumber.Value, matricula, temporaryPassword));
    }
}
