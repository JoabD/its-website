using Shekinah.Application.Abstractions;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.ManageChecklistItems;

/// <summary>
/// Configuración → Documentos de inscripción: catálogo editable de qué documentos pide el checklist
/// del panel de revisión (§3 de la spec). Todo protegido a Administrator — es configuración del
/// sistema, no operación del día a día.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record GetChecklistItemsQuery : IQuery<IReadOnlyList<ChecklistItemListItem>>;

public sealed record ChecklistItemListItem(string Id, string Label, int DisplayOrder, bool IsActive);

public sealed class GetChecklistItemsQueryHandler(IChecklistItemDefinitionRepository items)
    : IQueryHandler<GetChecklistItemsQuery, IReadOnlyList<ChecklistItemListItem>>
{
    public async Task<Result<IReadOnlyList<ChecklistItemListItem>>> HandleAsync(GetChecklistItemsQuery query, CancellationToken ct)
    {
        var all = await items.GetAllAsync(ct);
        IReadOnlyList<ChecklistItemListItem> mapped = all.Select(i => new ChecklistItemListItem(i.Id, i.Label, i.DisplayOrder, i.IsActive)).ToList();
        return Result.Success(mapped);
    }
}

[RequireRole(UserRole.Administrator)]
public sealed record CreateChecklistItemCommand(string Label) : ICommand<string>;

public sealed class CreateChecklistItemCommandHandler(IChecklistItemDefinitionRepository items)
    : ICommandHandler<CreateChecklistItemCommand, string>
{
    public async Task<Result<string>> HandleAsync(CreateChecklistItemCommand command, CancellationToken ct)
    {
        var existing = await items.GetAllAsync(ct);
        var nextOrder = existing.Count == 0 ? 1 : existing.Max(i => i.DisplayOrder) + 1;

        var result = ChecklistItemDefinition.Create(EntityId.NewId(), command.Label, nextOrder);
        if (result.IsFailure) return Result.Failure<string>(result.Error);

        await items.AddAsync(result.Value, ct);
        return Result.Success(result.Value.Id);
    }
}

[RequireRole(UserRole.Administrator)]
public sealed record UpdateChecklistItemCommand(string Id, string Label, int DisplayOrder) : ICommand<Abstractions.Unit>;

public sealed class UpdateChecklistItemCommandHandler(IChecklistItemDefinitionRepository items)
    : ICommandHandler<UpdateChecklistItemCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(UpdateChecklistItemCommand command, CancellationToken ct)
    {
        var item = await items.GetByIdAsync(command.Id, ct);
        if (item is null) return Result.Failure<Abstractions.Unit>(Error.NotFound("ChecklistItemDefinition.NotFound", "Documento no encontrado."));

        var renameResult = item.Rename(command.Label);
        if (renameResult.IsFailure) return Result.Failure<Abstractions.Unit>(renameResult.Error);

        item.Reorder(command.DisplayOrder);
        await items.UpdateAsync(item, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}

[RequireRole(UserRole.Administrator)]
public sealed record SetChecklistItemActiveCommand(string Id, bool IsActive) : ICommand<Abstractions.Unit>;

public sealed class SetChecklistItemActiveCommandHandler(IChecklistItemDefinitionRepository items)
    : ICommandHandler<SetChecklistItemActiveCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(SetChecklistItemActiveCommand command, CancellationToken ct)
    {
        var item = await items.GetByIdAsync(command.Id, ct);
        if (item is null) return Result.Failure<Abstractions.Unit>(Error.NotFound("ChecklistItemDefinition.NotFound", "Documento no encontrado."));

        if (command.IsActive) item.Activate();
        else item.Deactivate();

        await items.UpdateAsync(item, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
