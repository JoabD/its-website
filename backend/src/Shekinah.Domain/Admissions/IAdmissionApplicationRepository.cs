using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Admissions;

public interface IAdmissionApplicationRepository
{
    Task<AdmissionApplication?> GetByIdAsync(string id, CancellationToken ct);

    Task<AdmissionApplication?> GetByFolioAsync(string folio, CancellationToken ct);

    Task<(IReadOnlyList<AdmissionApplication> Items, long TotalCount)> SearchAsync(
        ApplicationStatus? status, string? searchText, int page, int pageSize, CancellationToken ct);

    Task AddAsync(AdmissionApplication application, CancellationToken ct);

    Task UpdateAsync(AdmissionApplication application, CancellationToken ct);

    Task DeleteAsync(string id, CancellationToken ct);

    Task<int> GetNextLegacySequenceAsync(CancellationToken ct);
}
