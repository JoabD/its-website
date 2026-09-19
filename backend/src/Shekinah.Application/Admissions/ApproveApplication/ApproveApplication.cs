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
public sealed record ApproveApplicationCommand(string ApplicationId, bool ViaQuickAction = false) : ICommand<ApproveApplicationResponse>, ITransactionalCommand;

public sealed record ApproveApplicationResponse(string UserId, int EnrollmentNumber, string Matricula, string TemporaryPassword);

public sealed class ApproveApplicationCommandHandler(
    IAdmissionApplicationRepository applications, IUserRepository users, IEnrollmentNumberGenerator enrollmentNumbers,
    IMatriculaGenerator matriculaGenerator, Domain.Catalog.IRegionRepository regions,
    IChecklistItemDefinitionRepository checklistItems,
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

        // RN de producto: la vía rápida (desde la lista) infiere que ya se tiene toda la
        // documentación y se salta el checklist; la vía "revisar" (desde el drawer) sí lo exige —
        // esto es una segunda validación en servidor, además del botón deshabilitado en el frontend.
        if (!command.ViaQuickAction)
        {
            var activeItems = await checklistItems.GetActiveAsync(ct);
            var isComplete = application.IsChecklistCompleteFor(activeItems.Select(i => i.Id).ToList());
            if (!isComplete)
            {
                return Result.Failure<ApproveApplicationResponse>(
                    Error.Validation("AdmissionApplication.ChecklistIncomplete", "Faltan documentos por verificar en el checklist."));
            }
        }

        var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
        var region = await regions.GetByIdAsync(application.ModalityChoice.RegionId, ct);
        var matricula = await matriculaGenerator.NextAsync(region?.Abbreviation ?? "ITS", ct);
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var profile = PersonalProfile.FromApplicant(application.Applicant);
        var regionRef = new RegionRef(application.ModalityChoice.RegionId, application.ModalityChoice.RegionCode, application.ModalityChoice.RegionName);

        var userResult = User.CreateStudentFromApplication(
            EntityId.NewId(), enrollmentNumber, matricula, application.Id, profile,
            application.ModalityChoice.Modality, regionRef, passwordHasher.Hash(temporaryPassword), clock);
        if (userResult.IsFailure)
        {
            return Result.Failure<ApproveApplicationResponse>(userResult.Error);
        }

        var decidedByName = "Administrador"; // Application obtiene el nombre real desde ICurrentUser en la implementación completa.
        var approveResult = application.Approve(currentUser.UserId ?? "system", decidedByName, userResult.Value.Id, clock, command.ViaQuickAction);
        if (approveResult.IsFailure)
        {
            return Result.Failure<ApproveApplicationResponse>(approveResult.Error);
        }

        await users.AddAsync(userResult.Value, ct);
        await applications.UpdateAsync(application, ct);

        await emailSender.SendAsync(
            profile.Email.Value,
            "Tus credenciales de acceso — Instituto Teológico Shekinah",
            $"<p>Matrícula: {matricula}</p><p>Contraseña temporal: {temporaryPassword}</p><p>Deberás cambiarla en tu primer inicio de sesión.</p>",
            ct);

        return Result.Success(new ApproveApplicationResponse(userResult.Value.Id, enrollmentNumber.Value, matricula, temporaryPassword));
    }
}
