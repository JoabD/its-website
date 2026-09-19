using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Admissions;

/// <summary>
/// Catálogo editable de documentos requeridos para inscribir a un solicitante (Configuración →
/// Documentos de inscripción). "Eliminar" desde la UI es siempre desactivar (<see cref="IsActive"/>
/// = false), nunca un borrado físico — así no se pierde el historial de qué se verificó en
/// solicitudes ya revisadas con ese ítem todavía activo. El checklist de cada
/// <see cref="AdmissionApplication"/> se evalúa siempre contra los ítems ACTIVOS de este catálogo
/// (opción "catálogo vivo", confirmada en spec).
/// </summary>
public sealed class ChecklistItemDefinition : AggregateRoot<string>
{
    private ChecklistItemDefinition() { }

    private ChecklistItemDefinition(string id, string label, int displayOrder, bool isActive) : base(id)
    {
        Label = label;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    public string Label { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<ChecklistItemDefinition> Create(string id, string label, int displayOrder, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure<ChecklistItemDefinition>(Error.Validation("ChecklistItemDefinition.LabelRequired", "El nombre del documento es requerido."));
        }

        return Result.Success(new ChecklistItemDefinition(id, label.Trim(), displayOrder, isActive));
    }

    public static ChecklistItemDefinition Rehydrate(string id, string label, int displayOrder, bool isActive) =>
        new(id, label, displayOrder, isActive);

    public Result Rename(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure(Error.Validation("ChecklistItemDefinition.LabelRequired", "El nombre del documento es requerido."));
        }

        Label = label.Trim();
        return Result.Success();
    }

    public void Reorder(int displayOrder) => DisplayOrder = displayOrder;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
