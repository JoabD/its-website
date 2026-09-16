using Shekinah.Domain.Academics.Events;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Academics;

/// <summary>
/// Agregado raíz Academics. Invariantes: <c>endsOn ≥ startsOn</c>; solo uno Active a la vez (RN-13,
/// reforzado también por índice único parcial en Mongo, spec técnico §5.3); un periodo cerrado no
/// se reabre; código único.
/// </summary>
public sealed class AcademicPeriod : AggregateRoot<string>
{
    private readonly List<MonthCode> _monthCodes = [];

    private AcademicPeriod() { }

    private AcademicPeriod(string id, PeriodCode code, string name, DateRange dateRange, IReadOnlyList<MonthCode> monthCodes, string openedByUserId, IClock clock)
        : base(id)
    {
        Code = code;
        Name = name;
        DateRange = dateRange;
        _monthCodes.AddRange(monthCodes);
        Status = AcademicPeriodStatus.Active;
        OpenedAtUtc = clock.UtcNow;
        OpenedByUserId = openedByUserId;
    }

    public PeriodCode Code { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public DateRange DateRange { get; private set; } = null!;

    /// <summary>Meses de cobro (RN-17), materializados al abrir el periodo con <see cref="Policies.AcademicPeriodMonthsCalculator"/>.</summary>
    public IReadOnlyList<MonthCode> MonthCodes => _monthCodes.AsReadOnly();

    public AcademicPeriodStatus Status { get; private set; }

    public DateTime OpenedAtUtc { get; private set; }

    public string OpenedByUserId { get; private set; } = string.Empty;

    public DateTime? ClosedAtUtc { get; private set; }

    public string? ClosedByUserId { get; private set; }

    /// <summary>
    /// RN-14 (parte 1 de 3, ver Domain Service <c>TermPromotionPolicy</c> para la orquestación completa):
    /// abre un periodo nuevo. La validación de "solo uno Active" la aplica el handler de Application
    /// consultando el repositorio antes de invocar este factory (el dominio no puede consultar Mongo).
    /// </summary>
    public static Result<AcademicPeriod> Open(
        string id, PeriodCode code, string name, DateRange dateRange, string openedByUserId, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<AcademicPeriod>(Error.Validation("AcademicPeriod.NameRequired", "El nombre del periodo es requerido."));
        }

        var monthCodes = Policies.AcademicPeriodMonthsCalculator.Calculate(dateRange, clock.UtcNow);
        var period = new AcademicPeriod(id, code, name.Trim(), dateRange, monthCodes, openedByUserId, clock);
        period.Raise(new AcademicPeriodOpened(Guid.NewGuid(), clock.UtcNow, id, code.Value));
        return Result.Success(period);
    }

    public static AcademicPeriod Rehydrate(
        string id, PeriodCode code, string name, DateRange dateRange, IReadOnlyList<MonthCode> monthCodes,
        AcademicPeriodStatus status, DateTime openedAtUtc, string openedByUserId, DateTime? closedAtUtc, string? closedByUserId)
    {
        var period = new AcademicPeriod
        {
            Id = id,
            Code = code,
            Name = name,
            DateRange = dateRange,
            Status = status,
            OpenedAtUtc = openedAtUtc,
            OpenedByUserId = openedByUserId,
            ClosedAtUtc = closedAtUtc,
            ClosedByUserId = closedByUserId,
        };
        period._monthCodes.AddRange(monthCodes);
        return period;
    }

    public Result Close(string closedByUserId, IClock clock)
    {
        if (Status == AcademicPeriodStatus.Closed)
        {
            return Result.Failure(Error.Conflict("AcademicPeriod.AlreadyClosed", "Un periodo cerrado no se reabre."));
        }

        Status = AcademicPeriodStatus.Closed;
        ClosedAtUtc = clock.UtcNow;
        ClosedByUserId = closedByUserId;
        return Result.Success();
    }
}
