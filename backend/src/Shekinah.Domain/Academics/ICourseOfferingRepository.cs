namespace Shekinah.Domain.Academics;

public interface ICourseOfferingRepository
{
    Task<CourseOffering?> GetByIdAsync(string id, CancellationToken ct);

    Task<CourseOffering?> FindAsync(string periodId, string subjectId, string regionId, CancellationToken ct);

    Task<IReadOnlyList<CourseOffering>> GetByPeriodAsync(string periodId, CancellationToken ct);

    Task<IReadOnlyList<CourseOffering>> GetByTeacherAndPeriodAsync(string teacherId, string periodId, CancellationToken ct);

    Task<IReadOnlyList<CourseOffering>> GetByStudentAndPeriodAsync(string studentId, string periodId, CancellationToken ct);

    Task AddAsync(CourseOffering offering, CancellationToken ct);

    Task UpdateAsync(CourseOffering offering, CancellationToken ct);

    Task DeleteAsync(string id, CancellationToken ct);
}
