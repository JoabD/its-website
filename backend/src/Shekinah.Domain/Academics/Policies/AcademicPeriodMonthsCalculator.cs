using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Academics.Policies;

/// <summary>
/// RN-17. Sustituye el CTE recursivo de MySQL (~150 líneas) por una función pura y testeada
/// (~15 líneas), tal como exige spec técnico §3.5. Los meses de cobro de un periodo son la serie
/// YYYYMM desde <c>startsOn</c> hasta <c>min(endsOn, mes actual)</c>, inclusive.
/// </summary>
public static class AcademicPeriodMonthsCalculator
{
    public static IReadOnlyList<MonthCode> Calculate(DateRange dateRange, DateTime nowUtc)
    {
        var start = MonthCode.FromYearMonth(dateRange.StartsOnUtc.Year, dateRange.StartsOnUtc.Month);

        var effectiveEndDate = dateRange.EndsOnUtc < nowUtc ? dateRange.EndsOnUtc : nowUtc;
        var end = MonthCode.FromYearMonth(effectiveEndDate.Year, effectiveEndDate.Month);

        return start.CompareTo(end) > 0 ? [start] : start.UpTo(end);
    }
}
