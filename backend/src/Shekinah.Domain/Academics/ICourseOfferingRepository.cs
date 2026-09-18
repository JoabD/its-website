namespace Shekinah.Domain.Academics;

public interface ICourseOfferingRepository
{
    Task<CourseOffering?> GetByIdAsync(string id, CancellationToken ct);

    Task<CourseOffering?> FindAsync(string periodId, string subjectId, string regionId, CancellationToken ct);

    Task<IReadOnlyList<CourseOffering>> GetByPeriodAsync(string periodId, CancellationToken ct);

    Task<IReadOnlyList<CourseOffering>> GetByTeacherAndPeriodAsync(string teacherId, string periodId, CancellationToken ct);

    Task<IReadOnlyList<CourseOffering>> GetByStudentAndPeriodAsync(string studentId, string periodId, CancellationToken ct);

    /// <summary>Historial completo del alumno, todos los periodos — usado por el Kardex (fase 8).</summary>
    Task<IReadOnlyList<CourseOffering>> GetByStudentAsync(string studentId, CancellationToken ct);

    Task AddAsync(CourseOffering offering, CancellationToken ct);

    Task UpdateAsync(CourseOffering offering, CancellationToken ct);

    Task DeleteAsync(string id, CancellationToken ct);
}
