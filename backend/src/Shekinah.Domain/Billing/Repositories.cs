using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Billing;

public interface IPaymentRepository
{
    Task<Payment?> FindAsync(string studentId, MonthCode monthCode, CancellationToken ct);

    Task<IReadOnlyList<MonthCode>> GetPaidMonthsAsync(string studentId, CancellationToken ct);

    Task AddAsync(Payment payment, CancellationToken ct);

    Task<PaymentImportBatch> RegisterImportBatchAsync(string fileName, string uploadedByUserId, int totalRows, CancellationToken ct);

    Task CompleteImportBatchAsync(string batchId, int importedRows, IReadOnlyList<PaymentImportRowError> errors, CancellationToken ct);

    Task<PaymentImportBatch?> GetImportBatchAsync(string batchId, CancellationToken ct);
}

public interface IPaymentNoticeRepository
{
    Task AddAsync(PaymentNotice notice, CancellationToken ct);

    Task<int> GetNextNoticeNumberAsync(string studentId, CancellationToken ct);

    Task<(IReadOnlyList<PaymentNotice> Items, long TotalCount)> GetHistoryAsync(string? studentId, int page, int pageSize, CancellationToken ct);
}

public sealed record PaymentImportRowError(int RowNumber, string Code, string Message, IReadOnlyList<string> RawValues);

public sealed record PaymentImportBatch(
    string Id, string FileName, string UploadedByUserId, DateTime UploadedAtUtc,
    int TotalRows, int ImportedRows, SharedKernel.ImportBatchStatus Status, IReadOnlyList<PaymentImportRowError> Errors);
