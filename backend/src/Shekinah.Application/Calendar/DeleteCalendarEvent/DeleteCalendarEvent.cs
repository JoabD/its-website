using Shekinah.Application.Abstractions;
using Shekinah.Domain.Calendar;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Calendar.DeleteCalendarEvent;

/// <summary>
/// Calendario institucional (panel admin visual) → eliminar evento. RN-09, igual que en la
/// creación: Administrator puede eliminar cualquier evento (general o de cualquier región);
/// RegionalCoordinator/RegionalSecretary solo pueden eliminar eventos de SU PROPIA región — nunca
/// eventos generales/institucionales ni de otra región — verificado en el handler vía
/// <see cref="IRegionScopeResolver"/>, nunca confiando en lo que mande el cliente.
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record DeleteCalendarEventCommand(string CalendarEventId) : ICommand<Abstractions.Unit>;

public sealed class DeleteCalendarEventCommandHandler(
    ICalendarEventRepository calendarEvents, IRegionScopeResolver regionScope)
    : ICommandHandler<DeleteCalendarEventCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(DeleteCalendarEventCommand command, CancellationToken ct)
    {
        var existing = await calendarEvents.GetByIdAsync(command.CalendarEventId, ct);
        if (existing is null)
        {
            return Result.Failure<Abstractions.Unit>(Error.NotFound("CalendarEvent.NotFound", "El evento no existe."));
        }

        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        if (mandatoryRegionId is not null && existing.Region?.Id != mandatoryRegionId)
        {
            return Result.Failure<Abstractions.Unit>(Error.Forbidden("CalendarEvent.Forbidden", "No puedes eliminar eventos de otra región."));
        }

        await calendarEvents.DeleteAsync(command.CalendarEventId, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
