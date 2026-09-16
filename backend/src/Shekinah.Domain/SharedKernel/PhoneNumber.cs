using System.Text.RegularExpressions;
using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>Teléfono mexicano a 10 dígitos (formato usado por el legado, sin lada internacional).</summary>
public sealed partial record PhoneNumber
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static Result<PhoneNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PhoneNumber>(Error.Validation("PhoneNumber.Empty", "El teléfono es requerido."));
        }

        var digits = DigitsOnly().Replace(value, string.Empty);

        if (digits.Length != 10)
        {
            return Result.Failure<PhoneNumber>(Error.Validation("PhoneNumber.Invalid", "El teléfono debe tener 10 dígitos."));
        }

        return Result.Success(new PhoneNumber(digits));
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnly();
}
