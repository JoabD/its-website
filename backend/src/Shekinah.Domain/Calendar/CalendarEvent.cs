using Shekinah.Domain.Common;

namespace Shekinah.Domain.Calendar;

public sealed record RegionRef(string Id, string Name);

/// <summary>
/// Evento del calendario institucional (plan de control escolar, fase 7). Público (se consulta
/// sin login, RN análoga a /catalog/curriculum): quien navega el sitio ve fechas de exámenes,
/// vacaciones y eventos sin necesitar cuenta. <see cref="Region"/> nulo = evento general/
/// institucional (visible para todos, sin distintivo de región); con región = solo aplica a esa
/// sede, y la UI le pone un distintivo de color propio de esa región.
/// </summary>
public sealed class CalendarEvent : AggregateRoot<string>
{
    private CalendarEvent() { }

    private CalendarEvent(
        string id, string title, string? description, DateTime startAtUtc, DateTime? endAtUtc,
        RegionRef? region, string createdByUserId, IClock clock)
        : base(id)
    {
        Title = title;
        Description = description;
        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
        Region = region;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = clock.UtcNow;
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime StartAtUtc { get; private set; }

    public DateTime? EndAtUtc { get; private set; }

    /// <summary>Nulo = evento general/institucional, visible para todas las regiones.</summary>
    public RegionRef? Region { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public static Result<CalendarEvent> Schedule(
        string id, string title, string? description, DateTime startAtUtc, DateTime? endAtUtc,
        RegionRef? region, string createdByUserId, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<CalendarEvent>(Error.Validation("CalendarEvent.TitleRequired", "El título del evento es requerido."));
        }

        if (endAtUtc is not null && endAtUtc < startAtUtc)
        {
            return Result.Failure<CalendarEvent>(Error.Validation("CalendarEvent.InvalidRange", "La fecha de fin no puede ser anterior a la de inicio."));
        }

        return Result.Success(new CalendarEvent(id, title.Trim(), description?.Trim(), startAtUtc, endAtUtc, region, createdByUserId, clock));
    }

    public static CalendarEvent Rehydrate(
        string id, string title, string? description, DateTime startAtUtc, DateTime? endAtUtc,
        RegionRef? region, string createdByUserId, DateTime createdAtUtc) => new()
    {
        Id = id,
        Title = title,
        Description = description,
        StartAtUtc = startAtUtc,
        EndAtUtc = endAtUtc,
        Region = region,
        CreatedByUserId = createdByUserId,
        CreatedAtUtc = createdAtUtc,
    };
}

public interface ICalendarEventRepository
{
    Task AddAsync(CalendarEvent calendarEvent, CancellationToken ct);

    /// <summary>Eventos desde <paramref name="fromUtc"/> en adelante, ordenados por fecha — usado
    /// tanto por la vista pública como por el panel admin.</summary>
    Task<IReadOnlyList<CalendarEvent>> GetUpcomingAsync(DateTime fromUtc, CancellationToken ct);
}
