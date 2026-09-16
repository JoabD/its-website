using System.Text.RegularExpressions;
using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

public sealed partial record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Email>(Error.Validation("Email.Empty", "El correo electrónico es requerido."));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 254 || !EmailRegex().IsMatch(normalized))
        {
            return Result.Failure<Email>(Error.Validation("Email.Invalid", "El correo electrónico no tiene un formato válido."));
        }

        return Result.Success(new Email(normalized));
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
