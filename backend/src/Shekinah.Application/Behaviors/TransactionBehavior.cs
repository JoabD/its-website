using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Behaviors;

/// <summary>
/// Abre sesión de Mongo solo para comandos marcados con <see cref="ITransactionalCommand"/>
/// (RN-05 y RN-14, spec técnico §3.7). Todo lo demás es single-document y por tanto atómico
/// por definición en MongoDB: abrir transacción ahí sería costo sin beneficio.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork) : IPipelineBehavior<TRequest, TResponse>
{
    public Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ITransactionalCommand)
        {
            return next();
        }

        return unitOfWork.ExecuteInTransactionAsync(_ => next(), ct);
    }
}
