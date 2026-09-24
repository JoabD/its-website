using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.RegisterManualPayment;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.GetBillingSummary;

/// <summary>
/// Panel de verificación de pagos (docs/Plan-Panel-Pagos.md, sección 8): resumen financiero de un
/// mes — cobrado vs. esperado (cuota fija × alumnos activos), desglosado por región. RN-09: mismo
/// forzado de alcance regional que GetPaymentMatrix — un RegionalCoordinator/Secretary solo ve su
/// propia región, inviolable desde el cliente.
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record GetBillingSummaryQuery(string MonthCode, string? RegionId) : IQuery<BillingSummaryResponse>;

public sealed record BillingSummaryRegionRow(
    string RegionId, string RegionName, int ActiveStudents, int PaidCount, int DueCount, decimal ExpectedTotal, decimal CollectedTotal);

public sealed record BillingSummaryResponse(
    string MonthCode, int ActiveStudents, int PaidCount, int DueCount,
    decimal ExpectedTotal, decimal CollectedTotal, double CollectionRatePercent,
    IReadOnlyList<BillingSummaryRegionRow> ByRegion);

public sealed class GetBillingSummaryQueryHandler(
    Domain.Identity.IUserRepository users, Domain.Billing.IPaymentRepository payments, IRegionScopeResolver regionScope)
    : IQueryHandler<GetBillingSummaryQuery, BillingSummaryResponse>
{
    private const decimal FixedMonthlyQuota = RegisterManualPaymentCommandHandler.FixedMonthlyQuota;

    public async Task<Result<BillingSummaryResponse>> HandleAsync(GetBillingSummaryQuery query, CancellationToken ct)
    {
        var monthCodeResult = MonthCode.Create(query.MonthCode);
        if (monthCodeResult.IsFailure)
        {
            return Result.Failure<BillingSummaryResponse>(monthCodeResult.Error);
        }

        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        var effectiveRegionId = mandatoryRegionId ?? query.RegionId;

        var students = await users.GetActiveStudentsInRegionAsync(effectiveRegionId ?? string.Empty, null, ct);

        var rows = new List<BillingSummaryRegionRow>();
        decimal expectedTotal = 0, collectedTotal = 0;
        int paidCount = 0;

        foreach (var group in students.GroupBy(s => (Id: s.Region?.Id ?? "sin-region", Name: s.Region?.Name ?? "Sin región")))
        {
            var groupExpected = 0m;
            var groupCollected = 0m;
            var groupPaid = 0;

            foreach (var student in group)
            {
                groupExpected += FixedMonthlyQuota;

                var payment = await payments.FindAsync(student.Id, monthCodeResult.Value, ct);
                if (payment is not null)
                {
                    groupPaid++;
                    groupCollected += payment.Amount?.Amount ?? FixedMonthlyQuota;
                }
            }

            rows.Add(new BillingSummaryRegionRow(group.Key.Id, group.Key.Name, group.Count(), groupPaid, group.Count() - groupPaid, groupExpected, groupCollected));

            expectedTotal += groupExpected;
            collectedTotal += groupCollected;
            paidCount += groupPaid;
        }

        var rate = expectedTotal == 0 ? 0d : (double)(collectedTotal / expectedTotal * 100m);

        return Result.Success(new BillingSummaryResponse(
            monthCodeResult.Value.Value, students.Count, paidCount, students.Count - paidCount,
            expectedTotal, collectedTotal, Math.Round(rate, 1),
            rows.OrderBy(r => r.RegionName).ToList()));
    }
}
