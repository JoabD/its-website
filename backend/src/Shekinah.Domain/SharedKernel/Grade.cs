using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>Calificación 0..10 (RN-16). Un entero, no un rango difuso como en el legado.</summary>
public sealed record Grade
{
    /// <summary>Mínimo para aprobar (2026-10, pedido explícito del cliente: "promover alumnos" por
    /// alumno debe ignorar a quien tenga una materia reprobada o sin calificar).</summary>
    public const int PassingValue = 7;

    public int Value { get; }

    private Grade(int value) => Value = value;

    public bool IsPassing => Value >= PassingValue;

    public static Result<Grade> Create(int value)
    {
        if (value is < 0 or > 10)
        {
            return Result.Failure<Grade>(Error.Validation("Grade.OutOfRange", "La calificación debe estar entre 0 y 10."));
        }

        return Result.Success(new Grade(value));
    }

    public override string ToString() => Value.ToString();
}
