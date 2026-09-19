using Shekinah.Application.Abstractions;
using Shekinah.Domain.Calendar;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Calendar.GetPublicCalendarEvents;

/// <summary>
/// Plan de control escolar, fase 7: el calendario institucional se consulta SIN necesidad de estar
/// logeado (confirmado por el usuario), análogo a /catalog/curriculum. <c>[AllowAnonymousUseCase]</c>
/// exime este caso de uso del requisito general de sesión que impone AuthorizationBehavior.
/// </summary>
[AllowAnonymousUseCase]
public sealed record GetPublicCalendarEventsQuery(DateTime? FromUtc, DateTime? ToUtc = null) : IQuery<IReadOnlyList<CalendarEventListItem>>;

public sealed record CalendarEventListItem(
    string Id, string Title, string? Description, DateTime StartAtUtc, DateTime? EndAtUtc,
    string? RegionId, string? RegionName);

public sealed class GetPublicCalendarEventsQueryHandler(ICalendarEventRepository calendarEvents, IClock clock)
    : IQueryHandler<GetPublicCalendarEventsQuery, IReadOnlyList<CalendarEventListItem>>
{
    // BUG REAL encontrado: "hoy" se calculaba con clock.UtcNow.Date (medianoche UTC), pero el
    // instituto opera en hora de México (Centro, UTC-6, sin horario de verano desde 2022 — huso
    // fijo). Como México va 6 horas detrás de UTC, entre las 6:00pm y la medianoche hora de México
    // el reloj UTC YA marca el día siguiente. Resultado: cualquier evento capturado con hora antes
    // de las 6:00pm (hora de México) del día actual, consultado después de las 6:00pm, calificaba
    // como "de ayer" según UTC y desaparecía del calendario público — justo lo que reportó el
    // usuario (evento de las 4:00pm capturado a las 9:50pm, mismo día calendario en México).
    // Se calcula la medianoche de "hoy" en hora de México y se convierte a UTC para el filtro.
    private static readonly TimeSpan InstitutionUtcOffset = TimeSpan.FromHours(-6);

    public async Task<Result<IReadOnlyList<CalendarEventListItem>>> HandleAsync(GetPublicCalendarEventsQuery query, CancellationToken ct)
    {
        var localNow = clock.UtcNow + InstitutionUtcOffset;
        var fromUtc = query.FromUtc ?? (localNow.Date - InstitutionUtcOffset);
        var items = await calendarEvents.GetUpcomingAsync(fromUtc, query.ToUtc, ct);

        IReadOnlyList<CalendarEventListItem> mapped = items
            .Select(e => new CalendarEventListItem(
                e.Id, e.Title, e.Description, e.StartAtUtc, e.EndAtUtc, e.Region?.Id, e.Region?.Name))
            .ToList();

        return Result.Success(mapped);
    }
}
