using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetGradingRoster;

/// <summary>
/// Rediseño de "Calificar por alumno" (2026-10, feedback real del cliente: el dropdown de alumnos
/// no servía porque no se veía nada hasta elegir uno, y no explicaba por qué un alumno salía sin
/// materias). Da, para TODO el periodo activo, cuántas materias tiene y cuántas ya calificó cada
/// alumno que aparece en al menos una oferta — así la tabla de "Calificar por alumno" puede mostrar
/// el avance sin una llamada por alumno (evita N+1).
///
/// Un alumno que no aparece en el resultado no tiene ninguna materia asignada en este periodo — el
/// frontend lo trata como 0/0 ("Sin materias"), que es justo el caso que generó la confusión
/// original (alumno con cuatrimestre/semestre asignado pero sin oferta con profesor todavía).
/// </summary>
[RequireRole(UserRole.Teacher, UserRole.Administrator)]
public sealed record GetGradingRosterQuery(string PeriodId) : IQuery<IReadOnlyList<StudentGradingSummaryRow>>;

public sealed record StudentGradingSummaryRow(string StudentId, int TotalSubjects, int GradedSubjects);

public sealed class GetGradingRosterQueryHandler(Domain.Academics.ICourseOfferingRepository offerings)
    : IQueryHandler<GetGradingRosterQuery, IReadOnlyList<StudentGradingSummaryRow>>
{
    public async Task<Result<IReadOnlyList<StudentGradingSummaryRow>>> HandleAsync(GetGradingRosterQuery query, CancellationToken ct)
    {
        var periodOfferings = await offerings.GetByPeriodAsync(query.PeriodId, ct);

        var byStudent = new Dictionary<string, (int Total, int Graded)>();
        foreach (var offering in periodOfferings)
        {
            foreach (var enrollment in offering.Enrollments)
            {
                var current = byStudent.TryGetValue(enrollment.StudentId, out var existing) ? existing : (Total: 0, Graded: 0);
                current.Total++;
                if (enrollment.Grade is not null)
                {
                    current.Graded++;
                }

                byStudent[enrollment.StudentId] = current;
            }
        }

        IReadOnlyList<StudentGradingSummaryRow> rows = byStudent
            .Select(kv => new StudentGradingSummaryRow(kv.Key, kv.Value.Total, kv.Value.Graded))
            .ToList();

        return Result.Success(rows);
    }
}
