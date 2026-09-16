using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Catalog.GetRegions;

[AllowAnonymousUseCase]
public sealed record GetRegionsQuery(Modality? Modality) : IQuery<IReadOnlyList<RegionListItem>>;

public sealed record RegionListItem(string Id, int Code, string Name, IReadOnlyList<Modality> ModalityScope);

public sealed class GetRegionsQueryHandler(Domain.Catalog.IRegionRepository regions) : IQueryHandler<GetRegionsQuery, IReadOnlyList<RegionListItem>>
{
    public async Task<Result<IReadOnlyList<RegionListItem>>> HandleAsync(GetRegionsQuery query, CancellationToken ct)
    {
        var all = query.Modality is null
            ? await regions.GetActiveAsync(ct)
            : await regions.GetActiveByModalityAsync(query.Modality.Value, ct);

        IReadOnlyList<RegionListItem> mapped = all.Select(r => new RegionListItem(r.Id, r.LegacyCode, r.Name, r.ModalityScope)).ToList();
        return Result.Success(mapped);
    }
}
