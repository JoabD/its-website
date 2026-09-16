using MongoDB.Driver;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Persistence;

/// <summary>
/// Sesiones/transacciones reales de Mongo (requiere replica set, PROMPT-MAESTRO.md Fase 0). Solo se
/// invoca para comandos ITransactionalCommand (RN-05, RN-14) vía TransactionBehavior — spec técnico §3.7.
/// </summary>
public sealed class MongoUnitOfWork(MongoContext context) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (_, innerCt) =>
        {
            await operation(innerCt);
            return true;
        }, cancellationToken: ct);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync((_, innerCt) => operation(innerCt), cancellationToken: ct);
    }
}
