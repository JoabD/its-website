using Shekinah.Application.Abstractions;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetStudentOfferings;

/// <summary>
/// "Calificar por alumno" (2026-10, pedido explícito del cliente): trae TODAS las materias
/// (ofertas) de un alumno en un periodo, para poblar la pantalla antes de capturar en lote — ver
/// <see cref="RecordStudentGradesBatch.RecordStudentGradesBatchCommand"/>. Reusa
/// <c>ICourseOfferingRepository.GetByStudentAndPeriodAsync</c> (ya existía, usado también por el
/// Kardex) en vez de crear una consulta nueva a Mongo.
///
/// RN pedida por el cliente (2026-10, feedback real): un alumno con cuatrimestre/semestre ya
/// asignado DEBE ver aquí el currículo completo de ese cuatrimestre/semestre — aunque todavía no
/// exista oferta (profesor asignado) para alguna materia. Antes, una materia sin oferta
/// simplemente no aparecía, lo cual el cliente reportó como "no entiendo por qué no tiene
/// materias si ya tiene cuatrimestre". Ahora esas materias SÍ aparecen, con <c>OfferingId</c> nulo
/// y <c>Status</c> "SinProfesor", para que la UI explique la causa real en vez de no mostrar nada.
/// Esto solo aplica para admin (un profesor sigue viendo únicamente SUS ofertas, RN-11 — mostrarle
/// materias sin profesor de otras academias no le aporta nada y puede confundir).
/// </summary>
public sealed record GetStudentOfferingsQuery(string StudentId, string PeriodId) : IQuery<IReadOnlyList<StudentOfferingRow>>;

public sealed record StudentOfferingRow(
    string? OfferingId, string SubjectName, string RegionName, int? TermNumber, int? Grade, string Status);

public sealed class GetStudentOfferingsQueryHandler(
    Domain.Academics.ICourseOfferingRepository offerings, ISubjectRepository subjects, IUserRepository users, ICurrentUser currentUser)
    : IQueryHandler<GetStudentOfferingsQuery, IReadOnlyList<StudentOfferingRow>>
{
    public async Task<Result<IReadOnlyList<StudentOfferingRow>>> HandleAsync(GetStudentOfferingsQuery query, CancellationToken ct)
    {
        var studentOfferings = await offerings.GetByStudentAndPeriodAsync(query.StudentId, query.PeriodId, ct);

        // RN-11: un profesor solo ve sus propias asignaciones — a diferencia de
        // GetOfferingEnrollmentsQuery (que rechaza con Forbidden si la oferta no es suya, porque ahí
        // solo hay UNA oferta de por medio), aquí simplemente se filtra: un profesor que entra a
        // "calificar por alumno" para alguien que también cursa materias con otros profesores
        // únicamente ve/califica las suyas, sin que eso le impida calificar las que sí le tocan.
        var visible = currentUser.Role == UserRole.Teacher
            ? studentOfferings.Where(o => o.Teacher is not null && o.Teacher.Id == currentUser.UserId).ToList()
            : studentOfferings.ToList();

        var rows = new List<StudentOfferingRow>();
        foreach (var offering in visible)
        {
            var enrollment = offering.Enrollments.First(e => e.StudentId == query.StudentId);
            rows.Add(new StudentOfferingRow(offering.Id, offering.Subject.Name, offering.Region.Name, offering.Subject.TermNumber, enrollment.Grade?.Value, enrollment.Status.ToString()));
        }

        if (currentUser.Role != UserRole.Teacher)
        {
            var student = await users.GetByIdAsync(query.StudentId, ct);
            if (student?.Academic is not null && !student.Academic.IsGraduated)
            {
                var curriculum = await subjects.GetByTermAndPlanAsync(student.Academic.CurrentTerm, student.Academic.Plan, ct);
                var coveredSubjectIds = visible.Select(o => o.Subject.Id).ToHashSet();
                foreach (var subject in curriculum)
                {
                    if (coveredSubjectIds.Contains(subject.Id))
                    {
                        continue;
                    }

                    rows.Add(new StudentOfferingRow(null, subject.Name, student.Region?.Name ?? "—", subject.TermNumber?.Value, null, "SinProfesor"));
                }
            }
        }

        IReadOnlyList<StudentOfferingRow> ordered = rows
            .OrderBy(r => r.TermNumber ?? int.MaxValue).ThenBy(r => r.SubjectName)
            .ToList();

        return Result.Success(ordered);
    }
}
