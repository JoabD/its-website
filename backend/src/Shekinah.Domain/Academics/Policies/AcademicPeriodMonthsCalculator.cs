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

    /// <summary>
    /// Panel de verificación de pagos (docs/Plan-Panel-Pagos.md): a diferencia de <see cref="Calculate"/>
    /// (que solo materializa meses YA transcurridos — correcto para RN-17/avisos de mora, nunca se
    /// debe notificar adeudo de un mes que todavía no llega), el panel de pagos necesita mostrar y
    /// dejar marcar TODO el cuatrimestre desde el día uno, para que el admin pueda adelantar pagos
    /// sin esperar a que cada mes "llegue". Misma fórmula, sin el tope de "hasta hoy".
    /// </summary>
    public static IReadOnlyList<MonthCode> CalculateFullRange(DateRange dateRange)
    {
        var start = MonthCode.FromYearMonth(dateRange.StartsOnUtc.Year, dateRange.StartsOnUtc.Month);
        var end = MonthCode.FromYearMonth(dateRange.EndsOnUtc.Year, dateRange.EndsOnUtc.Month);

        return start.CompareTo(end) > 0 ? [start] : start.UpTo(end);
    }
}
