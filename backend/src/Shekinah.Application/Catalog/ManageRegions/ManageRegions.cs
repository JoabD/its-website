using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Catalog.ManageRegions;

/// <summary>
/// Configuración → Regiones: a diferencia de <see cref="GetRegions.GetRegionsQuery"/> (pública, usada
/// por el wizard de admisión), esto es la vista/edición administrativa completa — incluye abreviatura
/// e IsActive, y permite reasignar qué modalidades sirve cada región (§4 de la spec: seguro de
/// cambiar en cualquier momento, sin candado por solicitudes en trámite — confirmado).
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record GetRegionsAdminQuery : IQuery<IReadOnlyList<RegionAdminListItem>>;

public sealed record RegionAdminListItem(string Id, int Code, string Name, string Abbreviation, IReadOnlyList<Modality> ModalityScope, bool IsActive);

public sealed class GetRegionsAdminQueryHandler(Domain.Catalog.IRegionRepository regions)
    : IQueryHandler<GetRegionsAdminQuery, IReadOnlyList<RegionAdminListItem>>
{
    public async Task<Result<IReadOnlyList<RegionAdminListItem>>> HandleAsync(GetRegionsAdminQuery query, CancellationToken ct)
    {
        var all = await regions.GetActiveAsync(ct);
        IReadOnlyList<RegionAdminListItem> mapped = all
            .OrderBy(r => r.Name)
            .Select(r => new RegionAdminListItem(r.Id, r.LegacyCode, r.Name, r.Abbreviation, r.ModalityScope, r.IsActive))
            .ToList();
        return Result.Success(mapped);
    }
}

[RequireRole(UserRole.Administrator)]
public sealed record UpdateRegionCommand(string RegionId, IReadOnlyList<Modality> ModalityScope, string Abbreviation) : ICommand<Abstractions.Unit>;

public sealed class UpdateRegionCommandHandler(Domain.Catalog.IRegionRepository regions) : ICommandHandler<UpdateRegionCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(UpdateRegionCommand command, CancellationToken ct)
    {
        var region = await regions.GetByIdAsync(command.RegionId, ct);
        if (region is null) return Result.Failure<Abstractions.Unit>(Error.NotFound("Region.NotFound", "Región no encontrada."));

        var scopeResult = region.SetModalityScope(command.ModalityScope);
        if (scopeResult.IsFailure) return Result.Failure<Abstractions.Unit>(scopeResult.Error);

        var abbreviationResult = region.SetAbbreviation(command.Abbreviation);
        if (abbreviationResult.IsFailure) return Result.Failure<Abstractions.Unit>(abbreviationResult.Error);

        await regions.UpdateAsync(region, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
