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

    /// <summary>Panel de Usuarios → "Eliminar usuario" (solo staff — nunca alumnos, ver
    /// DeleteUserCommandHandler). Borrado físico: los pocos campos que referencian un userId en otras
    /// colecciones (openedByUserId, resetByUserId, uploadedByUserId, etc.) son bitácora histórica en
    /// Mongo, no llaves foráneas — no hay integridad referencial que romper.</summary>
    Task DeleteAsync(string id, CancellationToken ct);

    Task<int> GetMaxEnrollmentNumberAsync(CancellationToken ct);

    /// <summary>Alta manual de alumnos por Excel (mismo patrón fila-a-fila que
    /// <c>IPaymentRepository</c>: reporta sin abortar el lote).</summary>
    Task<StudentImportBatch> RegisterStudentImportBatchAsync(string fileName, string uploadedByUserId, int totalRows, CancellationToken ct);

    Task CompleteStudentImportBatchAsync(string batchId, int importedRows, IReadOnlyList<StudentImportRowError> errors, CancellationToken ct);

    Task<StudentImportBatch?> GetStudentImportBatchAsync(string batchId, CancellationToken ct);
}

public sealed record StudentImportRowError(int RowNumber, string Code, string Message, IReadOnlyList<string> RawValues);

public sealed record StudentImportBatch(
    string Id, string FileName, string UploadedByUserId, DateTime UploadedAtUtc,
    int TotalRows, int ImportedRows, ImportBatchStatus Status, IReadOnlyList<StudentImportRowError> Errors);
