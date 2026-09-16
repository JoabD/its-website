namespace Shekinah.Application.Abstractions;

/// <summary>
/// Mediador CQRS propio y minimalista (PROMPT-MAESTRO.md §3 stack backend): ~120 líneas, sin
/// MediatR (su licencia dejó de ser libre para uso comercial desde la v13 — spec técnico §3.4).
/// </summary>
public interface ICommand<TResponse>;

public interface IQuery<TResponse>;

/// <summary>Marca los comandos que deben ejecutarse dentro de una sesión/transacción de Mongo (spec técnico §3.7).</summary>
public interface ITransactionalCommand;

public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Domain.Common.Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Domain.Common.Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct);
}

public delegate Task<Domain.Common.Result<TResponse>> RequestHandlerDelegate<TResponse>();

/// <summary>Orden de ejecución fijo (spec técnico §3.4): Logging → Authorization → Validation → Transaction → Handler.</summary>
public interface IPipelineBehavior<TRequest, TResponse>
{
    Task<Domain.Common.Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}

public interface IDispatcher
{
    Task<Domain.Common.Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct);

    Task<Domain.Common.Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct);
}
