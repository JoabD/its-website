namespace Shekinah.Domain.Common;

/// <summary>
/// Objeto de valor: inmutable, sin identidad propia, igualdad estructural. Las implementaciones
/// concretas del dominio se declaran como <c>sealed record</c> con un factory <c>Create(...)</c>
/// que devuelve <see cref="Result{TValue}"/> (nunca lanzan por datos inválidos de negocio).
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType()) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(17, (hash, component) => HashCode.Combine(hash, component));

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
