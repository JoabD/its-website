namespace Shekinah.Domain.Common;

/// <summary>
/// Raíz de agregado: única puerta de entrada para mutar el conjunto de entidades/VOs que protege.
/// "Un agregado = una transacción = un documento" (spec técnico §2.2). Acumula eventos de dominio
/// que Infrastructure despacha tras confirmar la escritura (outbox).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Control de concurrencia optimista (spec técnico §3.7): cada <c>ReplaceOne</c>/<c>UpdateOne</c>
    /// filtra por esta versión. Infrastructure la incrementa al persistir; el dominio no la muta.
    /// </summary>
    public int Version { get; protected set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Invocado por Infrastructure tras despachar los eventos al outbox.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
