using Shekinah.Application.Abstractions;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.UpdateChecklist;

/// <summary>Panel de revisión: guarda el progreso del checklist de documentación conforme el
/// administrador va verificando cada documento — así no se pierde si cierra el panel a medias.
/// <paramref name="CheckedItemIds"/> reemplaza por completo el conjunto marcado (catálogo vivo,
/// spec confirmada: se evalúa contra los ítems activos del catálogo, no una copia congelada).</summary>
[RequireRole(UserRole.Administrator)]
public sealed record UpdateChecklistCommand(string ApplicationId, IReadOnlyList<string> CheckedItemIds) : ICommand<Abstractions.Unit>;

public sealed class UpdateChecklistCommandHandler(IAdmissionApplicationRepository applications)
    : ICommandHandler<UpdateChecklistCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(UpdateChecklistCommand command, CancellationToken ct)
    {
        var application = await applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
        {
            return Result.Failure<Abstractions.Unit>(Error.NotFound("AdmissionApplication.NotFound", "Solicitud no encontrada."));
        }

        var result = application.UpdateChecklist(command.CheckedItemIds);
        if (result.IsFailure)
        {
            return Result.Failure<Abstractions.Unit>(result.Error);
        }

        await applications.UpdateAsync(application, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
