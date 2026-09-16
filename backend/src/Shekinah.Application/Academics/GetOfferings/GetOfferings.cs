using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetOfferings;

public sealed record GetOfferingsQuery(string PeriodId) : IQuery<IReadOnlyList<OfferingListItem>>;

public sealed record OfferingListItem(string Id, string SubjectName, string RegionName, string TeacherName, int EnrollmentCount);

/// <summary>RN-11: el profesor ve únicamente sus asignaciones del periodo activo.</summary>
public sealed class GetOfferingsQueryHandler(Domain.Academics.ICourseOfferingRepository offerings, ICurrentUser currentUser)
    : IQueryHandler<GetOfferingsQuery, IReadOnlyList<OfferingListItem>>
{
    public async Task<Result<IReadOnlyList<OfferingListItem>>> HandleAsync(GetOfferingsQuery query, CancellationToken ct)
    {
        var all = currentUser.Role == UserRole.Teacher && currentUser.UserId is not null
            ? await offerings.GetByTeacherAndPeriodAsync(currentUser.UserId, query.PeriodId, ct)
            : await offerings.GetByPeriodAsync(query.PeriodId, ct);

        IReadOnlyList<OfferingListItem> mapped = all
            .Select(o => new OfferingListItem(o.Id, o.Subject.Name, o.Region.Name, o.Teacher.Name, o.EnrollmentCount))
            .ToList();

        return Result.Success(mapped);
    }
}
