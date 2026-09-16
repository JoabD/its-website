using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>Código único de periodo escolar (PERIODO.PERIODO legado, numérico único).</summary>
public sealed record PeriodCode
{
    public string Value { get; }

    private PeriodCode(string value) => Value = value;

    public static Result<PeriodCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<PeriodCode>(Error.Validation("PeriodCode.Empty", "El código de periodo es requerido."));
        }

        return Result.Success(new PeriodCode(value.Trim()));
    }

    public override string ToString() => Value;
}
