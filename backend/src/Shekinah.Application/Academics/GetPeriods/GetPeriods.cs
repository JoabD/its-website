using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Academics.GetPeriods;

public sealed record GetPeriodsQuery(int Page, int PageSize) : IQuery<PagedResult<PeriodListItem>>;

public sealed record PeriodListItem(string Id, string Code, string Name, DateTime StartsOnUtc, DateTime EndsOnUtc, string Status);

public sealed class GetPeriodsQueryHandler(Domain.Academics.IAcademicPeriodRepository periods) : IQueryHandler<GetPeriodsQuery, PagedResult<PeriodListItem>>
{
    public async Task<Result<PagedResult<PeriodListItem>>> HandleAsync(GetPeriodsQuery query, CancellationToken ct)
    {
        var (items, total) = await periods.SearchAsync(query.Page, query.PageSize, ct);
        var mapped = items.Select(p => new PeriodListItem(p.Id, p.Code.Value, p.Name, p.DateRange.StartsOnUtc, p.DateRange.EndsOnUtc, p.Status.ToString())).ToList();
        return Result.Success(new PagedResult<PeriodListItem>(mapped, query.Page, query.PageSize, total));
    }
}
