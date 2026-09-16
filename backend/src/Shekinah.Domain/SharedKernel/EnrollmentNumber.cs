using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>
/// Matrícula: identificador numérico único e inmutable del usuario (USUARIOS.USUARIO legado).
/// Se genera con <c>IEnrollmentNumberGenerator</c> (Application) respaldado por un contador atómico
/// en Mongo (Infrastructure, findAndModify sobre <c>counters</c>).
/// </summary>
public sealed record EnrollmentNumber
{
    public int Value { get; }

    private EnrollmentNumber(int value) => Value = value;

    public static Result<EnrollmentNumber> Create(int value)
    {
        if (value <= 0)
        {
            return Result.Failure<EnrollmentNumber>(Error.Validation("EnrollmentNumber.Invalid", "La matrícula debe ser un entero positivo."));
        }

        return Result.Success(new EnrollmentNumber(value));
    }

    public override string ToString() => Value.ToString();
}
