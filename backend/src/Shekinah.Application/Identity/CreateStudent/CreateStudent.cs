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
    string FullName, string? Email, string? Phone, DateOnly BirthDate, string RegionId,
    Modality Modality, StudyPlan Plan, int CurrentTerm) : ICommand<CreateStudentResponse>;

public sealed record CreateStudentResponse(string UserId, int EnrollmentNumber, string Matricula, string TemporaryPassword);

public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        // Ajuste de flujo real: en la práctica el administrador no siempre tiene el correo ni el
        // teléfono del alumno al capturarlo, así que ninguno de los dos es obligatorio ya — pero si
        // se capturan, deben ser válidos. Sin correo, el alumno queda registrado (matrícula,
        // materias, calificaciones, pagos) pero sin acceso al sistema por ahora, ya que el login es
        // por correo. Sin teléfono, simplemente no se le puede ofrecer "enviar por WhatsApp".
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
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
        var termResult = TermNumber.Create(command.CurrentTerm);

        if (nameResult.IsFailure) return Result.Failure<CreateStudentResponse>(nameResult.Error);
        if (termResult.IsFailure) return Result.Failure<CreateStudentResponse>(termResult.Error);

        Email? emailValue = null;
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var emailResult = Email.Create(command.Email);
            if (emailResult.IsFailure) return Result.Failure<CreateStudentResponse>(emailResult.Error);
            emailValue = emailResult.Value;

            var existing = await users.GetByEmailAsync(emailValue.Value, ct);
            if (existing is not null)
            {
                return Result.Failure<CreateStudentResponse>(Error.Conflict("CreateStudent.EmailInUse", "Ya existe un usuario con ese correo."));
            }
        }

        PhoneNumber? phoneValue = null;
        if (!string.IsNullOrWhiteSpace(command.Phone))
        {
            var phoneResult = PhoneNumber.Create(command.Phone);
            if (phoneResult.IsFailure) return Result.Failure<CreateStudentResponse>(phoneResult.Error);
            phoneValue = phoneResult.Value;
        }

        var regionEntity = await regions.GetByIdAsync(command.RegionId, ct);
        if (regionEntity is null)
        {
            return Result.Failure<CreateStudentResponse>(Error.Validation("CreateStudent.InvalidRegion", "La región indicada no existe."));
        }

        if (!regionEntity.Serves(command.Modality))
        {
            return Result.Failure<CreateStudentResponse>(Error.Validation(
                "CreateStudent.ModalityNotServed",
                $"La región \"{regionEntity.Name}\" no ofrece la modalidad {ModalityLabel(command.Modality)}."));
        }

        var region = new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name);

        // Mismo respaldo que CreateUser.cs (alta de staff): los campos del expediente de admisión
        // (domicilio, iglesia, escolaridad) no se piden en el alta manual/rápida — se guardan como
        // "N/D" y el propio alumno los puede completar después desde "Mi perfil" si aplica.
        var address = Address.Create("N/D", "N/D", "N/D", "N/D").Value;
        var church = ChurchInfo.Create("N/D", address, "N/D", "N/D", MinistryRole.None).Value;
        var education = EducationLevel.Create(SchoolingLevel.Other, "N/D").Value;

        var profileResult = PersonalProfile.Create(
            nameResult.Value, emailValue, phoneValue, command.BirthDate, null, address, church, education, null, null);
        if (profileResult.IsFailure) return Result.Failure<CreateStudentResponse>(profileResult.Error);

        var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
        var matricula = await matriculaGenerator.NextAsync(regionEntity.Abbreviation, ct);
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var userResult = User.CreateStudentManually(
            EntityId.NewId(), enrollmentNumber, matricula, profileResult.Value, command.Modality, region,
            command.Plan, termResult.Value, passwordHasher.Hash(temporaryPassword), clock);
        if (userResult.IsFailure) return Result.Failure<CreateStudentResponse>(userResult.Error);

        await users.AddAsync(userResult.Value, ct);

        // Sin correo, no hay a quién enviarle credenciales — el alumno queda de alta sin acceso al
        // sistema por ahora (RN de flujo real: el administrador podrá capturar el correo después
        // desde "editar alumno" para habilitarle el acceso).
        if (emailValue is not null)
        {
            await emailSender.SendAsync(
                emailValue.Value,
                "Tus credenciales de acceso — Instituto Teológico Shekinah",
                $"<p>Matrícula: {matricula}</p><p>Contraseña temporal: {temporaryPassword}</p><p>Deberás cambiarla en tu primer inicio de sesión.</p>",
                ct);
        }

        return Result.Success(new CreateStudentResponse(userResult.Value.Id, enrollmentNumber.Value, matricula, temporaryPassword));
    }

    private static string ModalityLabel(Modality modality) => modality switch
    {
        Modality.Onsite => "Presencial",
        Modality.Online => "Virtual",
        Modality.Diploma => "Diplomado",
        _ => modality.ToString(),
    };
}
