using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Academics;

public interface IAcademicPeriodRepository
{
    Task<AcademicPeriod?> GetByIdAsync(string id, CancellationToken ct);

    Task<AcademicPeriod?> GetActiveAsync(CancellationToken ct);

    Task<(IReadOnlyList<AcademicPeriod> Items, long TotalCount)> SearchAsync(int page, int pageSize, CancellationToken ct);

    Task AddAsync(AcademicPeriod period, CancellationToken ct);

    Task UpdateAsync(AcademicPeriod period, CancellationToken ct);
}
