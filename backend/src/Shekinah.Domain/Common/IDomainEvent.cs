namespace Shekinah.Domain.Common;

/// <summary>
/// Marca un hecho de dominio ya ocurrido. Se acumula en el agregado y se despacha tras el commit
/// vía outbox (spec técnico §3.3, §5.2 outboxMessages). El dominio no sabe cómo se entrega.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
