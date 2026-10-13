using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetOfferings;

public sealed record GetOfferingsQuery(string PeriodId) : IQuery<IReadOnlyList<OfferingListItem>>;

public sealed record OfferingListItem(
    string Id, string SubjectName, string RegionName, string RegionId, string TeacherName, int EnrollmentCount,
    int? TermNumber, StudyPlan Plan);

/// <summary>RN-11: el profesor ve únicamente sus asignaciones del periodo activo. Incluye
/// cuatrimestre/semestre y plan de la materia (join contra el catálogo) para que la pantalla de
/// administración pueda separar Cuatrimestral de Semestral (plan de control escolar / Kardex, fase 2).</summary>
public sealed class GetOfferingsQueryHandler(
    Domain.Academics.ICourseOfferingRepository offerings, Domain.Catalog.ISubjectRepository subjects, ICurrentUser currentUser)
    : IQueryHandler<GetOfferingsQuery, IReadOnlyList<OfferingListItem>>
{
    public async Task<Result<IReadOnlyList<OfferingListItem>>> HandleAsync(GetOfferingsQuery query, CancellationToken ct)
    {
        var all = currentUser.Role == UserRole.Teacher && currentUser.UserId is not null
            ? await offerings.GetByTeacherAndPeriodAsync(currentUser.UserId, query.PeriodId, ct)
            : await offerings.GetByPeriodAsync(query.PeriodId, ct);

        var curriculum = await subjects.GetCurriculumAsync(ct);
        var plansBySubjectId = curriculum.ToDictionary(s => s.Id, s => s.Plan);

        IReadOnlyList<OfferingListItem> mapped = all
            .Select(o => new OfferingListItem(
                o.Id, o.Subject.Name, o.Region.Name, o.Region.Id, o.Teacher.Name, o.EnrollmentCount,
                o.Subject.TermNumber, plansBySubjectId.GetValueOrDefault(o.Subject.Id, StudyPlan.Quarterly)))
            .ToList();

        return Result.Success(mapped);
    }
}
