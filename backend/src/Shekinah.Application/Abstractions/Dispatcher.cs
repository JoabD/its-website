using Microsoft.Extensions.DependencyInjection;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Abstractions;

/// <summary>
/// Implementación por reflexión + DI del mediador propio. Compone la cadena de behaviors en el
/// orden fijo Logging → Authorization → Validation → Transaction → Handler (spec técnico §3.4).
/// Registrada en el composition root (Shekinah.Api) junto con el escaneo de ensamblado de handlers.
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken ct) =>
        Invoke<TResponse>(command, typeof(ICommandHandler<,>));

    public Task<Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken ct) =>
        Invoke<TResponse>(query, typeof(IQueryHandler<,>));

    private Task<Result<TResponse>> Invoke<TResponse>(object request, Type openHandlerType)
    {
        var requestType = request.GetType();
        var handlerType = openHandlerType.MakeGenericType(requestType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        var handleMethod = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"El handler {handlerType.Name} no expone HandleAsync.");

        RequestHandlerDelegate<TResponse> callHandler = () =>
            (Task<Result<TResponse>>)handleMethod.Invoke(handler, [request, CancellationToken.None])!;

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = serviceProvider.GetServices(behaviorType).Cast<object>().ToList();

        var pipeline = behaviors
            .AsEnumerable()
            .Reverse()
            .Aggregate(callHandler, (next, behavior) =>
            {
                var handleBehaviorMethod = behaviorType.GetMethod("HandleAsync")!;
                return () => (Task<Result<TResponse>>)handleBehaviorMethod.Invoke(behavior, [request, next, CancellationToken.None])!;
            });

        return pipeline();
    }
}
