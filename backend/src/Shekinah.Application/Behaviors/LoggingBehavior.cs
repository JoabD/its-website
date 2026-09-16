using Microsoft.Extensions.Logging;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Ejecutando {UseCase}", name);

        var result = await next();

        if (result.IsFailure)
        {
            logger.LogWarning("{UseCase} falló: {ErrorCode} - {ErrorMessage}", name, result.Error.Code, result.Error.Message);
        }
        else
        {
            logger.LogInformation("{UseCase} completado", name);
        }

        return result;
    }
}
