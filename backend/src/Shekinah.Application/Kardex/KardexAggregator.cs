using Shekinah.Application.Abstractions;
using Shekinah.Application.Kardex.GetStudentKardex;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Kardex;

/// <summary>
/// Lógica compartida entre <c>GetStudentKardexQueryHandler</c> y <c>SendKardexEmailCommandHandler</c>
/// (fase 8): resolver a qué alumno se refiere "me"/un id concreto respetando RN-09 y el
/// autoservicio del alumno, y construir las filas de materias/promedio a partir de sus
/// CourseOffering. Evita duplicar esta regla en dos handlers.
/// </summary>
internal static class KardexAggregator
{
    private const int PassingGrade = 6;

    public static async Task<Result<User>> ResolveAuthorizedStudentAsync(
        string requestedStudentId, IUserRepository users, IRegionScopeResolver regionScope, ICurrentUser currentUser, CancellationToken ct)
    {
        var studentId = requestedStudentId == "me" ? currentUser.UserId : requestedStudentId;
        if (studentId is null)
        {
            return Result.Failure<User>(Error.Unauthorized("Auth.Required", "Se requiere autenticación."));
        }

        if (currentUser.Role == UserRole.Student && studentId != currentUser.UserId)
        {
            return Result.Failure<User>(Error.Forbidden("Kardex.Forbidden", "Solo puedes consultar tu propio Kardex."));
        }

        var student = await users.GetByIdAsync(studentId, ct);
        if (student is null || student.Role != UserRole.Student)
        {
            return Result.Failure<User>(Error.NotFound("Kardex.StudentNotFound", "Alumno no encontrado."));
        }

        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        if (mandatoryRegionId is not null && student.Region?.Id != mandatoryRegionId)
        {
            return Result.Failure<User>(Error.Forbidden("Kardex.OutOfScope", "Este alumno no pertenece a tu región."));
        }

        return Result.Success(student);
    }

    public static async Task<(IReadOnlyList<KardexSubjectRow> Subjects, double? AverageGrade)> BuildSubjectsAsync(
        User student, ICourseOfferingRepository offerings, CancellationToken ct)
    {
        var courseOfferings = await offerings.GetByStudentAsync(student.Id, ct);

        var subjectRows = courseOfferings
            .Select(o => (Offering: o, Enrollment: o.Enrollments.First(e => e.StudentId == student.Id)))
            .OrderBy(x => x.Offering.Subject.TermNumber ?? int.MaxValue)
            .ThenBy(x => x.Offering.Subject.Name)
            .Select(x => new KardexSubjectRow(
                x.Offering.Subject.Name, x.Offering.Subject.TermNumber, x.Enrollment.Grade?.Value,
                DescribeStatus(x.Enrollment.Status.ToString(), x.Enrollment.Grade?.Value),
                x.Offering.Period.Code))
            .ToList();

        var gradedValues = subjectRows.Where(s => s.Grade is not null).Select(s => s.Grade!.Value).ToList();
        double? average = gradedValues.Count > 0 ? gradedValues.Average() : null;

        return (subjectRows, average);
    }

    private static string DescribeStatus(string enrollmentStatus, int? grade)
    {
        if (enrollmentStatus == "Dropped") return "Baja";
        if (grade is null) return "En curso";
        return grade >= PassingGrade ? "Aprobada" : "No aprobada";
    }
}
