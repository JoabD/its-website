using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Catalog;

/// <summary>Interfaz segregada (ISP, spec técnico §4): solo lo que la aplicación realmente usa.</summary>
public interface IRegionRepository
{
    Task<Region?> GetByIdAsync(string id, CancellationToken ct);

    Task<IReadOnlyList<Region>> GetActiveAsync(CancellationToken ct);

    Task<IReadOnlyList<Region>> GetActiveByModalityAsync(Modality modality, CancellationToken ct);

    Task AddAsync(Region region, CancellationToken ct);

    Task UpdateAsync(Region region, CancellationToken ct);
}
