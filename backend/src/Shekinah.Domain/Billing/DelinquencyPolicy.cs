using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Billing;

/// <summary>
/// RN-19: calcula los meses adeudados de un alumno en el periodo activo comparando los meses de
/// cobro del periodo (RN-17, ya materializados en <c>AcademicPeriod.MonthCodes</c>) contra los
/// meses efectivamente pagados. Función pura, sin acceso a base de datos.
/// </summary>
public static class DelinquencyPolicy
{
    public static IReadOnlyList<MonthCode> CalculateMonthsDue(IReadOnlyList<MonthCode> periodMonthCodes, IReadOnlyCollection<MonthCode> paidMonthCodes) =>
        periodMonthCodes.Where(m => !paidMonthCodes.Contains(m)).OrderBy(m => m).ToList();

    public static bool ShouldNotify(IReadOnlyList<MonthCode> monthsDue) => monthsDue.Count > 0;

    /// <summary>Avisos 1–3 son advertencia; al superar 3 el usuario queda bloqueado (RN-19, reflejado también en Identity.BillingState).</summary>
    public static bool WillResultInBlock(int nextNoticeNumber) => nextNoticeNumber > Identity.BillingState.MaxNoticesBeforeBlock;
}
