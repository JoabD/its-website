using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.GetApplications;

[RequireRole(UserRole.Administrator)]
public sealed record GetApplicationsQuery(ApplicationStatus? Status, string? SearchText, int Page, int PageSize) : IQuery<PagedResult<ApplicationListItem>>;

public sealed record ApplicationListItem(
    string Id, string Folio, string ApplicantName, Modality Modality, string RegionName, ApplicationStatus Status,
    DateTime SubmittedAtUtc, DateTime? PurgeScheduledAtUtc);

public sealed class GetApplicationsQueryHandler(Domain.Admissions.IAdmissionApplicationRepository applications)
    : IQueryHandler<GetApplicationsQuery, PagedResult<ApplicationListItem>>
{
    public async Task<Result<PagedResult<ApplicationListItem>>> HandleAsync(GetApplicationsQuery query, CancellationToken ct)
    {
        var (items, total) = await applications.SearchAsync(query.Status, query.SearchText, query.Page, query.PageSize, ct);

        var mapped = items.Select(a => new ApplicationListItem(
            a.Id, a.Folio, a.Applicant.FullName.FullName, a.ModalityChoice.Modality, a.ModalityChoice.RegionName, a.Status,
            a.SubmittedAtUtc, a.PurgeScheduledAtUtc)).ToList();

        return Result.Success(new PagedResult<ApplicationListItem>(mapped, query.Page, query.PageSize, total));
    }
}
