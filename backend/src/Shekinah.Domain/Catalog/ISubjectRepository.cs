using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Catalog;

public interface ISubjectRepository
{
    Task<Subject?> GetByIdAsync(string id, CancellationToken ct);

    Task<Subject?> GetByCodeAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<Subject>> GetCurriculumAsync(CancellationToken ct);

    Task<IReadOnlyList<Subject>> GetByTermAsync(TermNumber termNumber, CancellationToken ct);

    Task AddAsync(Subject subject, CancellationToken ct);

    Task UpdateAsync(Subject subject, CancellationToken ct);

    Task DeleteAsync(string id, CancellationToken ct);
}
