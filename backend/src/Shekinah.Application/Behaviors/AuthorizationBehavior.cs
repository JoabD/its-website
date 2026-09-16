using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Behaviors;

/// <summary>
/// RN-08/RN-12: todo caso de uso es [Authorize] por defecto salvo <see cref="AllowAnonymousUseCaseAttribute"/>
/// (corrige hallazgo de auditoría #1: rutas POST sensibles sin autenticación en el legado).
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
{
    public Task<Result<TResponse>> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var type = typeof(TRequest);
        var allowAnonymous = Attribute.IsDefined(type, typeof(AllowAnonymousUseCaseAttribute));

        if (!allowAnonymous)
        {
            if (!currentUser.IsAuthenticated)
            {
                return Task.FromResult(Result.Failure<TResponse>(Error.Unauthorized("Auth.Required", "Se requiere autenticación.")));
            }

            var requireRole = (RequireRoleAttribute?)Attribute.GetCustomAttribute(type, typeof(RequireRoleAttribute));
            if (requireRole is not null && (currentUser.Role is null || !requireRole.Roles.Contains(currentUser.Role.Value)))
            {
                return Task.FromResult(Result.Failure<TResponse>(Error.Forbidden("Auth.Forbidden", "No tiene permisos para esta operación.")));
            }
        }

        return next();
    }
}
