using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Catalog;

/// <summary>
/// Región (RN-03/RN-09). El mapeo modalidad→región del legado (hardcodeado en inscripcion.js)
/// se convierte en dato de configuración editable: <see cref="ModalityScope"/> (ver PROMPT-MAESTRO.md
/// §11, ADR "Regiones").
/// </summary>
public sealed class Region : AggregateRoot<string>
{
    private readonly List<Modality> _modalityScope = [];

    private Region() { }

    private Region(string id, int legacyCode, string name, IEnumerable<Modality> modalityScope, string abbreviation) : base(id)
    {
        LegacyCode = legacyCode;
        Name = name;
        _modalityScope.AddRange(modalityScope);
        IsActive = true;
        Abbreviation = abbreviation;
    }

    public int LegacyCode { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyList<Modality> ModalityScope => _modalityScope.AsReadOnly();

    public bool IsActive { get; private set; }

    /// <summary>
    /// Siglas cortas (2-3 letras) usadas para armar la matrícula de alumnos aprobados
    /// (formato ITS/{Abbreviation}/{consecutivo}, ver <c>IMatriculaGenerator</c>). Editable desde el
    /// catálogo de regiones; si queda vacía, se deriva automáticamente del nombre como respaldo
    /// (nunca debe bloquear la aprobación de una solicitud por falta de configuración).
    /// </summary>
    public string Abbreviation { get; private set; } = string.Empty;

    public static Result<Region> Create(string id, int legacyCode, string name, IEnumerable<Modality> modalityScope, string? abbreviation = null)
    {
        var scope = modalityScope.Distinct().ToList();

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Region>(Error.Validation("Region.NameRequired", "El nombre de la región es requerido."));
        }

        if (scope.Count == 0)
        {
            return Result.Failure<Region>(Error.Validation("Region.NoScope", "Una región debe servir al menos una modalidad."));
        }

        var resolvedAbbreviation = string.IsNullOrWhiteSpace(abbreviation) ? DeriveAbbreviation(name) : abbreviation.Trim().ToUpperInvariant();

        return Result.Success(new Region(id, legacyCode, name.Trim(), scope, resolvedAbbreviation));
    }

    public bool Serves(Modality modality) => IsActive && _modalityScope.Contains(modality);

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Configuración → Regiones: permite reasignar qué modalidades sirve una región (ej. una región
    /// que era solo Virtual pasa a ofrecer también Presencial). Seguro de cambiar en cualquier
    /// momento: la modalidad de un alumno ya inscrito y la de solicitudes ya decididas quedan fijas
    /// en su propio registro (nunca se recalculan desde la región), así que esto solo afecta qué
    /// modalidades se ofrecen para solicitudes NUEVAS a partir de ahora (spec confirmada: sin
    /// restricción adicional, ni siquiera si hay solicitudes Pending en la modalidad anterior).
    /// </summary>
    public Result SetModalityScope(IEnumerable<Modality> modalityScope)
    {
        var scope = modalityScope.Distinct().ToList();
        if (scope.Count == 0)
        {
            return Result.Failure(Error.Validation("Region.NoScope", "Una región debe servir al menos una modalidad."));
        }

        _modalityScope.Clear();
        _modalityScope.AddRange(scope);
        return Result.Success();
    }

    public Result SetAbbreviation(string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(abbreviation))
        {
            return Result.Failure(Error.Validation("Region.AbbreviationRequired", "La abreviatura es requerida."));
        }

        Abbreviation = abbreviation.Trim().ToUpperInvariant();
        return Result.Success();
    }

    /// <summary>Respaldo cuando no hay abreviatura configurada: primeras letras de cada palabra
    /// significativa del nombre (ej. "Región San Miguel" -> "SM"), nunca vacío.</summary>
    private static string DeriveAbbreviation(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !string.Equals(w, "Región", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var initials = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        return initials.Length == 0 ? "XX" : initials[..Math.Min(initials.Length, 3)];
    }
}
