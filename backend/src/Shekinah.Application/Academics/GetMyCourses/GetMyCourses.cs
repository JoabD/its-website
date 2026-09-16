using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Academics.GetMyCourses;

/// <summary>RN-10: el alumno ve únicamente sus materias del periodo activo.</summary>
public sealed record GetMyCoursesQuery : IQuery<IReadOnlyList<StudentCourseRow>>;

public sealed record StudentCourseRow(string OfferingId, string SubjectName, string TeacherName, string RegionName, string PeriodCode, int? Grade);

public sealed class GetMyCoursesQueryHandler(
    Domain.Academics.ICourseOfferingRepository offerings, Domain.Academics.IAcademicPeriodRepository periods, ICurrentUser currentUser)
    : IQueryHandler<GetMyCoursesQuery, IReadOnlyList<StudentCourseRow>>
{
    public async Task<Result<IReadOnlyList<StudentCourseRow>>> HandleAsync(GetMyCoursesQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is null)
        {
            return Result.Failure<IReadOnlyList<StudentCourseRow>>(Error.Unauthorized("Auth.Required", "Se requiere autenticación."));
        }

        var activePeriod = await periods.GetActiveAsync(ct);
        if (activePeriod is null)
        {
            return Result.Success<IReadOnlyList<StudentCourseRow>>([]);
        }

        var myOfferings = await offerings.GetByStudentAndPeriodAsync(currentUser.UserId, activePeriod.Id, ct);

        IReadOnlyList<StudentCourseRow> rows = myOfferings.Select(o =>
        {
            var enrollment = o.Enrollments.First(e => e.StudentId == currentUser.UserId);
            return new StudentCourseRow(o.Id, o.Subject.Name, o.Teacher.Name, o.Region.Name, o.Period.Code, enrollment.Grade?.Value);
        }).ToList();

        return Result.Success(rows);
    }
}
