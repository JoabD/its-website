using Shekinah.Application.Abstractions;
using Shekinah.Application.Identity.CreateUser;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.ApproveApplication;

/// <summary>
/// RN-05: en UNA SOLA transacción, aprueba la solicitud, crea el User (matrícula secuencial,
/// Student, cuatrimestre 1, mustChangePassword=true, contraseña temporal) y encola el correo de
/// credenciales. Por eso implementa <see cref="ITransactionalCommand"/> (spec técnico §3.7).
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record ApproveApplicationCommand(string ApplicationId) : ICommand<ApproveApplicationResponse>, ITransactionalCommand;

public sealed record ApproveApplicationResponse(string UserId, int EnrollmentNumber, string TemporaryPassword);

public sealed class ApproveApplicationCommandHandler(
    IAdmissionApplicationRepository applications, IUserRepository users, IEnrollmentNumberGenerator enrollmentNumbers,
    IPasswordHasher passwordHasher, IEmailSender emailSender, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ApproveApplicationCommand, ApproveApplicationResponse>
{
    public async Task<Result<ApproveApplicationResponse>> HandleAsync(ApproveApplicationCommand command, CancellationToken ct)
    {
        var application = await applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
        {
            return Result.Failure<ApproveApplicationResponse>(Error.NotFound("AdmissionApplication.NotFound", "Solicitud no encontrada."));
        }

        var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var profile = PersonalProfile.FromApplicant(application.Applicant);
        var region = new RegionRef(application.ModalityChoice.RegionId, application.ModalityChoice.RegionCode, application.ModalityChoice.RegionName);

        var userResult = User.CreateStudentFromApplication(
            EntityId.NewId(), enrollmentNumber, application.Id, profile,
            application.ModalityChoice.Modality, region, passwordHasher.Hash(temporaryPassword), clock);
        if (userResult.IsFailure)
        {
            return Result.Failure<ApproveApplicationResponse>(userResult.Error);
        }

        var decidedByName = "Administrador"; // Application obtiene el nombre real desde ICurrentUser en la implementación completa.
        var approveResult = application.Approve(currentUser.UserId ?? "system", decidedByName, userResult.Value.Id, clock);
        if (approveResult.IsFailure)
        {
            return Result.Failure<ApproveApplicationResponse>(approveResult.Error);
        }

        await users.AddAsync(userResult.Value, ct);
        await applications.UpdateAsync(application, ct);

        await emailSender.SendAsync(
            profile.Email.Value,
            "Tus credenciales de acceso — Instituto Teológico Shekinah",
            $"<p>Matrícula: {enrollmentNumber}</p><p>Contraseña temporal: {temporaryPassword}</p><p>Deberás cambiarla en tu primer inicio de sesión.</p>",
            ct);

        return Result.Success(new ApproveApplicationResponse(userResult.Value.Id, enrollmentNumber.Value, temporaryPassword));
    }
}
