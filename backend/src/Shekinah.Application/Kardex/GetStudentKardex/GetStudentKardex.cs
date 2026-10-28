using Shekinah.Application.Abstractions;
using Shekinah.Application.Kardex;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Kardex.GetStudentKardex;

/// <summary>
/// Plan de control escolar, fase 8: Kardex del alumno — materias por cuatrimestre, calificaciones,
/// estatus, promedio general, todo sobre datos que ya existen (CourseOffering/Enrollment), sin
/// nuevas entidades de dominio. <paramref name="StudentId"/> acepta el literal "me" para que el
/// propio alumno consulte el suyo sin conocer su Id interno (mismo patrón que /users/me).
/// </summary>
[RequireRole(UserRole.Student, UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record GetStudentKardexQuery(string StudentId) : IQuery<KardexResponse>;

/// <summary>
/// <paramref name="OfferingId"/> va null cuando la fila no corresponde a una oferta real editable
/// (no debería pasar en la práctica, ya que toda fila sale de un CourseOffering) — se deja
/// nullable solo por si en el futuro el Kardex agrega filas sintéticas. El panel admin lo usa para
/// poder capturar/corregir la calificación de esa materia directamente desde el detalle del alumno
/// (plan de control escolar, fase "flujo de asignación", 2026-10), sin tener que ir a Calificaciones
/// a buscar el grupo correspondiente.
/// </summary>
public sealed record KardexSubjectRow(string SubjectName, int? TermNumber, int? Grade, string Status, string PeriodCode, string? OfferingId = null);

public sealed record KardexResponse(
    string StudentId, int EnrollmentNumber, string FullName, string? Email, string? RegionName,
    Modality? Modality, int? CurrentTerm, DateTime EnrolledAtUtc, bool IsGraduated,
    double? AverageGrade, IReadOnlyList<KardexSubjectRow> Subjects,
    IReadOnlyList<Shekinah.Application.Kardex.KardexTermGroup> Terms, bool HasPendingSubjects);

public sealed class GetStudentKardexQueryHandler(
    Domain.Identity.IUserRepository users, Domain.Academics.ICourseOfferingRepository offerings,
    IRegionScopeResolver regionScope, ICurrentUser currentUser)
    : IQueryHandler<GetStudentKardexQuery, KardexResponse>
{
    public async Task<Result<KardexResponse>> HandleAsync(GetStudentKardexQuery query, CancellationToken ct)
    {
        var studentResult = await KardexAggregator.ResolveAuthorizedStudentAsync(query.StudentId, users, regionScope, currentUser, ct);
        if (studentResult.IsFailure)
        {
            return Result.Failure<KardexResponse>(studentResult.Error);
        }

        var student = studentResult.Value;
        var (subjects, average) = await KardexAggregator.BuildSubjectsAsync(student, offerings, ct);
        var terms = KardexAggregator.GroupByTerm(subjects);
        var hasPending = terms.Any(t => t.HasPendingSubject);

        var response = new KardexResponse(
            student.Id, student.EnrollmentNumber.Value, student.Profile.FullName.FullName, student.Profile.Email?.Value,
            student.Region?.Name, student.Modality, student.Academic?.CurrentTerm.Value,
            student.Academic?.EnrolledAtUtc ?? student.CreatedAtUtc, student.Academic?.IsGraduated ?? false,
            average, subjects, terms, hasPending);

        return Result.Success(response);
    }
}
