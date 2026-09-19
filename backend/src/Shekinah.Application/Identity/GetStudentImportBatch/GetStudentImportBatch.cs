using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.GetStudentImportBatch;

[RequireRole(UserRole.Administrator)]
public sealed record GetStudentImportBatchQuery(string BatchId) : IQuery<StudentImportBatch>;

public sealed class GetStudentImportBatchQueryHandler(IUserRepository users) : IQueryHandler<GetStudentImportBatchQuery, StudentImportBatch>
{
    public async Task<Result<StudentImportBatch>> HandleAsync(GetStudentImportBatchQuery query, CancellationToken ct)
    {
        var batch = await users.GetStudentImportBatchAsync(query.BatchId, ct);
        return batch is null
            ? Result.Failure<StudentImportBatch>(Error.NotFound("StudentImportBatch.NotFound", "Lote de importación no encontrado."))
            : Result.Success(batch);
    }
}
