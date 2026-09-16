using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

public sealed record PersonName
{
    public string FullName { get; }

    private PersonName(string fullName) => FullName = fullName;

    public static Result<PersonName> Create(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure<PersonName>(Error.Validation("PersonName.Empty", "El nombre completo es requerido."));
        }

        var trimmed = string.Join(' ', fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (trimmed.Length is < 3 or > 200)
        {
            return Result.Failure<PersonName>(Error.Validation("PersonName.Length", "El nombre debe tener entre 3 y 200 caracteres."));
        }

        return Result.Success(new PersonName(trimmed));
    }

    public override string ToString() => FullName;
}
