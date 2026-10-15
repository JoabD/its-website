using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetGradeImportTemplate;

/// <summary>
/// Kardex → Calificaciones → "Importar desde Excel" → "Descargar plantilla": una plantilla por
/// (periodo, plan, cuatrimestre/semestre, región), con TODAS las materias de ese grupo como
/// columnas y los alumnos ya inscritos prellenados (plan de control escolar / Kardex, fase 2).
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record GetGradeImportTemplateQuery(string PeriodId, StudyPlan Plan, int TermNumber, string RegionId) : IQuery<GradeImportTemplateResponse>;

public sealed record GradeImportTemplateResponse(string FileName, string ContentBase64);

public sealed class GetGradeImportTemplateQueryHandler(
    Domain.Academics.IAcademicPeriodRepository periods, Domain.Catalog.ISubjectRepository subjects,
    Domain.Catalog.IRegionRepository regions, Domain.Academics.ICourseOfferingRepository offerings,
    ISpreadsheetWriter writer)
    : IQueryHandler<GetGradeImportTemplateQuery, GradeImportTemplateResponse>
{
    public async Task<Result<GradeImportTemplateResponse>> HandleAsync(GetGradeImportTemplateQuery query, CancellationToken ct)
    {
        var period = await periods.GetByIdAsync(query.PeriodId, ct);
        if (period is null)
        {
            return Result.Failure<GradeImportTemplateResponse>(Error.NotFound("AcademicPeriod.NotFound", "Periodo no encontrado."));
        }

        var region = await regions.GetByIdAsync(query.RegionId, ct);
        if (region is null)
        {
            return Result.Failure<GradeImportTemplateResponse>(Error.NotFound("Region.NotFound", "Región no encontrada."));
        }

        var termResult = TermNumber.Create(query.TermNumber);
        if (termResult.IsFailure)
        {
            return Result.Failure<GradeImportTemplateResponse>(termResult.Error);
        }

        var termSubjects = await subjects.GetByTermAndPlanAsync(termResult.Value, query.Plan, ct);
        if (termSubjects.Count == 0)
        {
            return Result.Failure<GradeImportTemplateResponse>(Error.Validation(
                "GetGradeImportTemplate.NoSubjects", "No hay materias dadas de alta para este plan y cuatrimestre/semestre."));
        }

        var periodOfferings = await offerings.GetByPeriodAsync(query.PeriodId, ct);
        var subjectIds = termSubjects.Select(s => s.Id).ToHashSet();
        var relevantOfferings = periodOfferings
            .Where(o => subjectIds.Contains(o.Subject.Id) && o.Region.Id == query.RegionId)
            .ToList();

        var students = relevantOfferings
            .SelectMany(o => o.Enrollments)
            .Where(e => e.Status == EnrollmentStatus.Active)
            .GroupBy(e => e.StudentId)
            .Select(g => new GradeImportTemplateStudent(g.First().EnrollmentNumber, g.First().StudentName))
            .OrderBy(s => s.EnrollmentNumber)
            .ToList();

        var subjectColumns = termSubjects.Select(s => new GradeImportTemplateSubject(s.Name)).ToList();
        var planLabel = query.Plan == StudyPlan.Semester ? "Semestral" : "Cuatrimestral";

        var bytes = writer.BuildGradeImportTemplate(period.Name, planLabel, query.TermNumber, region.Name, subjectColumns, students);
        var fileName = $"Plantilla_Calificaciones_{(planLabel == "Semestral" ? "Semestre" : "Cuatrimestre")}{query.TermNumber}_{region.Abbreviation}.xlsx";

        return Result.Success(new GradeImportTemplateResponse(fileName, Convert.ToBase64String(bytes)));
    }
}
