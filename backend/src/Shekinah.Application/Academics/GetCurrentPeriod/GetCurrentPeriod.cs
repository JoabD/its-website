using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Academics.GetCurrentPeriod;

public sealed record GetCurrentPeriodQuery : IQuery<CurrentPeriodResponse?>;

public sealed record CurrentPeriodResponse(string Id, string Code, string Name, DateTime StartsOnUtc, DateTime EndsOnUtc, IReadOnlyList<string> MonthCodes, IReadOnlyList<string> AllMonthCodes);

public sealed class GetCurrentPeriodQueryHandler(Domain.Academics.IAcademicPeriodRepository periods) : IQueryHandler<GetCurrentPeriodQuery, CurrentPeriodResponse?>
{
    public async Task<Result<CurrentPeriodResponse?>> HandleAsync(GetCurrentPeriodQuery query, CancellationToken ct)
    {
        var active = await periods.GetActiveAsync(ct);
        if (active is null)
        {
            return Result.Success<CurrentPeriodResponse?>(null);
        }

        var allMonthCodes = Domain.Academics.Policies.AcademicPeriodMonthsCalculator.CalculateFullRange(active.DateRange);

        return Result.Success<CurrentPeriodResponse?>(new CurrentPeriodResponse(
            active.Id, active.Code.Value, active.Name, active.DateRange.StartsOnUtc, active.DateRange.EndsOnUtc,
            active.MonthCodes.Select(m => m.Value).ToList(),
            allMonthCodes.Select(m => m.Value).ToList()));
    }
}
