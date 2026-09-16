using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>
/// Cuatrimestre del alumno: 1..6, o <see cref="Graduated"/> (conserva el código legado 10, ver
/// PROMPT-MAESTRO.md §2.4 USUARIOS.CUATRIMESTRE).
/// </summary>
public sealed record TermNumber
{
    public const int GraduatedCode = 10;

    public int Value { get; }

    public bool IsGraduated => Value == GraduatedCode;

    private TermNumber(int value) => Value = value;

    public static readonly TermNumber Graduated = new(GraduatedCode);

    public static Result<TermNumber> Create(int value)
    {
        if (value != GraduatedCode && value is < 1 or > 6)
        {
            return Result.Failure<TermNumber>(Error.Validation("TermNumber.OutOfRange", "El cuatrimestre debe estar entre 1 y 6, o ser Egresado."));
        }

        return Result.Success(new TermNumber(value));
    }

    public static TermNumber First => new(1);

    /// <summary>Promueve un cuatrimestre según RN-14: 6 ⇒ Graduated, en otro caso +1.</summary>
    public TermNumber Promote() => Value == 6 ? Graduated : new TermNumber(Value + 1);

    public override string ToString() => IsGraduated ? "Egresado" : Value.ToString();
}
