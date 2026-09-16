namespace Shekinah.Domain.Common;

/// <summary>
/// Puerto de tiempo. El dominio nunca llama a <c>DateTime.UtcNow</c> directamente: eso lo haría
/// imposible de testear de forma determinista. Implementado en Infrastructure, inyectado donde
/// haga falta (política, servicio de dominio, factory).
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
