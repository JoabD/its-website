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
public sealed record GetPublicCalendarEventsQuery(DateTime? FromUtc) : IQuery<IReadOnlyList<CalendarEventListItem>>;

public sealed record CalendarEventListItem(
    string Id, string Title, string? Description, DateTime StartAtUtc, DateTime? EndAtUtc,
    string? RegionId, string? RegionName);

public sealed class GetPublicCalendarEventsQueryHandler(ICalendarEventRepository calendarEvents, IClock clock)
    : IQueryHandler<GetPublicCalendarEventsQuery, IReadOnlyList<CalendarEventListItem>>
{
    public async Task<Result<IReadOnlyList<CalendarEventListItem>>> HandleAsync(GetPublicCalendarEventsQuery query, CancellationToken ct)
    {
        var fromUtc = query.FromUtc ?? clock.UtcNow.Date;
        var items = await calendarEvents.GetUpcomingAsync(fromUtc, ct);

        IReadOnlyList<CalendarEventListItem> mapped = items
            .Select(e => new CalendarEventListItem(
                e.Id, e.Title, e.Description, e.StartAtUtc, e.EndAtUtc, e.Region?.Id, e.Region?.Name))
            .ToList();

        return Result.Success(mapped);
    }
}
