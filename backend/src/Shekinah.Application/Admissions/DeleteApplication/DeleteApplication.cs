using Shekinah.Application.Abstractions;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.DeleteApplication;

/// <summary>
/// "Eliminar" desde la bandeja (decisión de producto explícita): borrado físico, permitido SOLO
/// mientras la solicitud siga Pending. Una vez Approved/Rejected es inmutable y NO se puede
/// eliminar — ahí ya hay una decisión (y posiblemente un User creado) que debe quedar trazable.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record DeleteApplicationCommand(string ApplicationId) : ICommand<Abstractions.Unit>;

public sealed class DeleteApplicationCommandHandler(IAdmissionApplicationRepository applications)
    : ICommandHandler<DeleteApplicationCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(DeleteApplicationCommand command, CancellationToken ct)
    {
        var application = await applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
        {
            return Result.Failure<Abstractions.Unit>(Error.NotFound("AdmissionApplication.NotFound", "Solicitud no encontrada."));
        }

        if (!application.IsDeletable)
        {
            return Result.Failure<Abstractions.Unit>(Error.Conflict(
                "AdmissionApplication.NotDeletable", "Solo se pueden eliminar solicitudes pendientes; esta ya fue decidida."));
        }

        await applications.DeleteAsync(command.ApplicationId, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
