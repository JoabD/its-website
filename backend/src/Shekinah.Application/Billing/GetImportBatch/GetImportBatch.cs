using Shekinah.Application.Abstractions;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.GetImportBatch;

[RequireRole(UserRole.Administrator)]
public sealed record GetImportBatchQuery(string BatchId) : IQuery<PaymentImportBatch>;

public sealed class GetImportBatchQueryHandler(IPaymentRepository payments) : IQueryHandler<GetImportBatchQuery, PaymentImportBatch>
{
    public async Task<Result<PaymentImportBatch>> HandleAsync(GetImportBatchQuery query, CancellationToken ct)
    {
        var batch = await payments.GetImportBatchAsync(query.BatchId, ct);
        return batch is null
            ? Result.Failure<PaymentImportBatch>(Error.NotFound("ImportBatch.NotFound", "Lote de importación no encontrado."))
            : Result.Success(batch);
    }
}
