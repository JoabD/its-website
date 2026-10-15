using Shekinah.Application.Abstractions;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetGradingRoster;

/// <summary>
/// Rediseño de "Calificar por alumno" (2026-10, feedback real del cliente: el dropdown de alumnos
/// no servía porque no se veía nada hasta elegir uno, y no explicaba por qué un alumno salía sin
/// materias). Da, para TODO el periodo activo, cuántas materias tiene y cuántas ya calificó cada
/// alumno.
///
/// RN pedida por el cliente (2026-10, segunda vuelta de feedback): "Total" ya NO es solo el conteo
/// de materias con oferta — es el currículo completo del cuatrimestre/semestre que el alumno ya
/// tiene asignado (<see cref="ISubjectRepository.GetByTermAndPlanAsync"/>), aunque alguna de esas
/// materias todavía no tenga oferta/profesor. Así, un alumno con término asignado pero SIN ninguna
/// oferta creada deja de verse como "0/0 — Sin materias" (que generó la confusión original) y pasa
/// a verse como, p. ej., "0/8 — pendientes de profesor", reflejando el currículo real en vez de
/// únicamente lo que ya se dio de alta en Académico.
/// </summary>
[RequireRole(UserRole.Teacher, UserRole.Administrator)]
public sealed record GetGradingRosterQuery(string PeriodId) : IQuery<IReadOnlyList<StudentGradingSummaryRow>>;

public sealed record StudentGradingSummaryRow(string StudentId, int TotalSubjects, int GradedSubjects);

public sealed class GetGradingRosterQueryHandler(
    Domain.Academics.ICourseOfferingRepository offerings, IUserRepository users, ISubjectRepository subjects)
    : IQueryHandler<GetGradingRosterQuery, IReadOnlyList<StudentGradingSummaryRow>>
{
    public async Task<Result<IReadOnlyList<StudentGradingSummaryRow>>> HandleAsync(GetGradingRosterQuery query, CancellationToken ct)
    {
        var periodOfferings = await offerings.GetByPeriodAsync(query.PeriodId, ct);

        var byStudent = new Dictionary<string, (int Total, int Graded, HashSet<string> SubjectIds)>();
        foreach (var offering in periodOfferings)
        {
            foreach (var enrollment in offering.Enrollments)
            {
                if (!byStudent.TryGetValue(enrollment.StudentId, out var current))
                {
                    current = (0, 0, new HashSet<string>());
                }

                current.Total++;
                if (enrollment.Grade is not null)
                {
                    current.Graded++;
                }

                current.SubjectIds.Add(offering.Subject.Id);
                byStudent[enrollment.StudentId] = current;
            }
        }

        // Completa el "Total" con el currículo del plan+cuatrimestre/semestre de cada alumno, para
        // las materias que todavía no tienen oferta. Se cachea por (plan, término) porque son a lo
        // mucho ~12 combinaciones reales — evita pedir el mismo currículo una vez por alumno.
        var (allStudents, _) = await users.SearchAsync(UserRole.Student, null, null, 1, 10_000, ct);
        var curriculumCache = new Dictionary<(StudyPlan Plan, int Term), IReadOnlyList<Subject>>();

        foreach (var student in allStudents)
        {
            if (student.Academic is null || student.Academic.IsGraduated)
            {
                continue;
            }

            var key = (student.Academic.Plan, student.Academic.CurrentTerm.Value);
            if (!curriculumCache.TryGetValue(key, out var curriculum))
            {
                curriculum = await subjects.GetByTermAndPlanAsync(student.Academic.CurrentTerm, student.Academic.Plan, ct);
                curriculumCache[key] = curriculum;
            }

            if (curriculum.Count == 0)
            {
                continue;
            }

            if (!byStudent.TryGetValue(student.Id, out var current))
            {
                current = (0, 0, new HashSet<string>());
            }

            var missing = curriculum.Count(s => !current.SubjectIds.Contains(s.Id));
            if (missing > 0)
            {
                current.Total += missing;
                byStudent[student.Id] = current;
            }
        }

        IReadOnlyList<StudentGradingSummaryRow> rows = byStudent
            .Select(kv => new StudentGradingSummaryRow(kv.Key, kv.Value.Total, kv.Value.Graded))
            .ToList();

        return Result.Success(rows);
    }
}
