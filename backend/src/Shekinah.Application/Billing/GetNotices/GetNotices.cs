using Shekinah.Application.Abstractions;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.GetNotices;

[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator)]
public sealed record GetNoticesQuery(string? StudentId, int Page, int PageSize) : IQuery<PagedResult<NoticeListItem>>;

public sealed record NoticeListItem(string Id, int EnrollmentNumber, string StudentName, int NoticeNumber, IReadOnlyList<string> MonthsDue, DateTime IssuedAtUtc, bool ResultedInBlock);

public sealed class GetNoticesQueryHandler(IPaymentNoticeRepository notices) : IQueryHandler<GetNoticesQuery, PagedResult<NoticeListItem>>
{
    public async Task<Result<PagedResult<NoticeListItem>>> HandleAsync(GetNoticesQuery query, CancellationToken ct)
    {
        var (items, total) = await notices.GetHistoryAsync(query.StudentId, query.Page, query.PageSize, ct);
        var mapped = items.Select(n => new NoticeListItem(
            n.Id, n.Student.EnrollmentNumber, n.Student.Name, n.NoticeNumber, n.MonthsDue.Select(m => m.Value).ToList(), n.IssuedAtUtc, n.ResultedInBlock)).ToList();

        return Result.Success(new PagedResult<NoticeListItem>(mapped, query.Page, query.PageSize, total));
    }
}
