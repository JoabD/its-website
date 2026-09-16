using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>
/// Escolaridad. El legado guardaba 3 flags booleanos (PRIMARIA/SECUNDARIA/BACHILLERATO) más texto
/// libre para "otra". Se colapsa en un único VO explícito (PROMPT-MAESTRO.md §5.10).
/// </summary>
public sealed record EducationLevel
{
    public SchoolingLevel Level { get; }

    public string? OtherDescription { get; }

    private EducationLevel(SchoolingLevel level, string? otherDescription)
    {
        Level = level;
        OtherDescription = otherDescription;
    }

    public static Result<EducationLevel> Create(SchoolingLevel level, string? otherDescription)
    {
        if (level == SchoolingLevel.Other && string.IsNullOrWhiteSpace(otherDescription))
        {
            return Result.Failure<EducationLevel>(Error.Validation(
                "EducationLevel.MissingDescription",
                "Debe especificar la descripción de la escolaridad cuando el nivel es 'Otra'."));
        }

        return Result.Success(new EducationLevel(level, level == SchoolingLevel.Other ? otherDescription!.Trim() : null));
    }
}
