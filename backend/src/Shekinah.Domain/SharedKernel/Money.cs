using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>Monto monetario. Nunca <c>double</c> (spec técnico §5.1); en Mongo se serializa Decimal128.</summary>
public sealed record Money
{
    public decimal Amount { get; }

    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency = "MXN")
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(Error.Validation("Money.Negative", "El monto no puede ser negativo."));
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return Result.Failure<Money>(Error.Validation("Money.Currency", "La moneda debe ser un código ISO de 3 letras."));
        }

        return Result.Success(new Money(amount, currency.ToUpperInvariant()));
    }
}
