using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Catalog.GetCurriculum;

/// <summary>RN-20: catálogo público (sitio institucional), [anónimo].</summary>
[AllowAnonymousUseCase]
public sealed record GetCurriculumQuery : IQuery<IReadOnlyList<CurriculumSubject>>;

public sealed record CurriculumSubject(string Id, string Code, string Name, string ProgramType, int? TermNumber, int DisplayOrder, bool HasSyllabus, string? SyllabusPdfUrl);

public sealed class GetCurriculumQueryHandler(Domain.Catalog.ISubjectRepository subjects)
    : IQueryHandler<GetCurriculumQuery, IReadOnlyList<CurriculumSubject>>
{
    public async Task<Result<IReadOnlyList<CurriculumSubject>>> HandleAsync(GetCurriculumQuery query, CancellationToken ct)
    {
        var all = await subjects.GetCurriculumAsync(ct);

        IReadOnlyList<CurriculumSubject> mapped = all
            .OrderBy(s => s.ProgramType).ThenBy(s => s.TermNumber?.Value ?? 0).ThenBy(s => s.DisplayOrder)
            .Select(s => new CurriculumSubject(
                s.Id, s.Code, s.Name, s.ProgramType.ToString(), s.TermNumber?.Value, s.DisplayOrder,
                s.Syllabus.IsPublished, s.Syllabus.PdfUrl))
            .ToList();

        return Result.Success(mapped);
    }
}
