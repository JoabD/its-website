using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Identity;

/// <summary>
/// Interfaz segregada por caso de uso real (ISP), no un <c>IRepository&lt;T&gt;</c> genérico
/// (spec técnico §4-I).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id, CancellationToken ct);

    Task<User?> GetByEnrollmentNumberAsync(EnrollmentNumber enrollmentNumber, CancellationToken ct);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    Task<IReadOnlyList<User>> GetActiveAdministratorsAsync(CancellationToken ct);

    Task<(IReadOnlyList<User> Items, long TotalCount)> SearchAsync(
        UserRole? role, string? regionId, string? searchText, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<User>> GetActiveStudentsInRegionAsync(string regionId, TermNumber? currentTerm, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);

    Task UpdateAsync(User user, CancellationToken ct);

    Task<int> GetMaxEnrollmentNumberAsync(CancellationToken ct);
}
