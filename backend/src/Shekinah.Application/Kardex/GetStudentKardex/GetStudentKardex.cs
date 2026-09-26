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

public sealed record KardexSubjectRow(string SubjectName, int? TermNumber, int? Grade, string Status, string PeriodCode);

public sealed record KardexResponse(
    string StudentId, int EnrollmentNumber, string FullName, string? Email, string? RegionName,
    Modality? Modality, int? CurrentTerm, DateTime EnrolledAtUtc, bool IsGraduated,
    double? AverageGrade, IReadOnlyList<KardexSubjectRow> Subjects);

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

        var response = new KardexResponse(
            student.Id, student.EnrollmentNumber.Value, student.Profile.FullName.FullName, student.Profile.Email?.Value,
            student.Region?.Name, student.Modality, student.Academic?.CurrentTerm.Value,
            student.Academic?.EnrolledAtUtc ?? student.CreatedAtUtc, student.Academic?.IsGraduated ?? false,
            average, subjects);

        return Result.Success(response);
    }
}
