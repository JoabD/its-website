using Microsoft.AspNetCore.Diagnostics;

namespace Shekinah.Api.Middleware;

/// <summary>
/// Corrige el hallazgo de auditoría #3 (dd($e-&gt;getMessage()) en producción): captura toda excepción
/// no manejada, la registra con Serilog (con traceId) y responde ProblemDetails genérico — NUNCA
/// exception.Message al cliente.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Excepción no controlada procesando {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://its-shekinah.edu.mx/problems/internal-error",
            title = "Failure.Unexpected",
            status = 500,
            detail = "Ocurrió un error inesperado. Intente de nuevo más tarde.",
            traceId = httpContext.TraceIdentifier,
        }, cancellationToken);

        return true;
    }
}
