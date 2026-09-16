using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Abstractions;

/// <summary>
/// Autorización basada en políticas por caso de uso (RN-08: "la autorización se evalúa siempre en
/// servidor"). Se declara sobre el Command/Query; <see cref="Behaviors.AuthorizationBehavior{TRequest,TResponse}"/>
/// la hace cumplir ANTES de llegar al handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequireRoleAttribute(params UserRole[] roles) : Attribute
{
    public UserRole[] Roles { get; } = roles;
}

/// <summary>Marca explícita de que el caso de uso es accesible sin autenticación (contrato §7 [anónimo]).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AllowAnonymousUseCaseAttribute : Attribute;
