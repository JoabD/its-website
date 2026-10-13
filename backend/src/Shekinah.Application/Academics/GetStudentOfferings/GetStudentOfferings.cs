using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetStudentOfferings;

/// <summary>
/// "Calificar por alumno" (2026-10, pedido explícito del cliente): trae TODAS las materias
/// (ofertas) de un alumno en un periodo, para poblar la pantalla antes de capturar en lote — ver
/// <see cref="RecordStudentGradesBatch.RecordStudentGradesBatchCommand"/>. Reusa
/// <c>ICourseOfferingRepository.GetByStudentAndPeriodAsync</c> (ya existía, usado también por el
/// Kardex) en vez de crear una consulta nueva a Mongo.
/// </summary>
public sealed record GetStudentOfferingsQuery(string StudentId, string PeriodId) : IQuery<IReadOnlyList<StudentOfferingRow>>;

public sealed record StudentOfferingRow(
    string OfferingId, string SubjectName, string RegionName, int? TermNumber, int? Grade, string Status);

public sealed class GetStudentOfferingsQueryHandler(Domain.Academics.ICourseOfferingRepository offerings, ICurrentUser currentUser)
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
            ? studentOfferings.Where(o => o.Teacher.Id == currentUser.UserId).ToList()
            : studentOfferings;

        IReadOnlyList<StudentOfferingRow> rows = visible
            .Select(o =>
            {
                var enrollment = o.Enrollments.First(e => e.StudentId == query.StudentId);
                return new StudentOfferingRow(o.Id, o.Subject.Name, o.Region.Name, o.Subject.TermNumber, enrollment.Grade?.Value, enrollment.Status.ToString());
            })
            .OrderBy(r => r.TermNumber ?? int.MaxValue).ThenBy(r => r.SubjectName)
            .ToList();

        return Result.Success(rows);
    }
}
