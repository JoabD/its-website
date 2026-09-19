namespace Shekinah.Domain.Admissions;

public interface IChecklistItemDefinitionRepository
{
    Task<ChecklistItemDefinition?> GetByIdAsync(string id, CancellationToken ct);

    /// <summary>Todos los ítems (activos e inactivos), ordenados por DisplayOrder — para la pantalla
    /// de Configuración, donde el administrador también ve y puede reactivar los desactivados.</summary>
    Task<IReadOnlyList<ChecklistItemDefinition>> GetAllAsync(CancellationToken ct);

    /// <summary>Solo los activos, ordenados por DisplayOrder — el catálogo "vivo" contra el que se
    /// evalúa si el checklist de una solicitud está completo.</summary>
    Task<IReadOnlyList<ChecklistItemDefinition>> GetActiveAsync(CancellationToken ct);

    Task AddAsync(ChecklistItemDefinition item, CancellationToken ct);

    Task UpdateAsync(ChecklistItemDefinition item, CancellationToken ct);
}
