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

    private Region(string id, int legacyCode, string name, IEnumerable<Modality> modalityScope) : base(id)
    {
        LegacyCode = legacyCode;
        Name = name;
        _modalityScope.AddRange(modalityScope);
        IsActive = true;
    }

    public int LegacyCode { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyList<Modality> ModalityScope => _modalityScope.AsReadOnly();

    public bool IsActive { get; private set; }

    public static Result<Region> Create(string id, int legacyCode, string name, IEnumerable<Modality> modalityScope)
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

        return Result.Success(new Region(id, legacyCode, name.Trim(), scope));
    }

    public bool Serves(Modality modality) => IsActive && _modalityScope.Contains(modality);

    public void Deactivate() => IsActive = false;
}
