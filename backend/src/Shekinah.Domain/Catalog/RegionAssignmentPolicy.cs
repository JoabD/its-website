using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Catalog;

/// <summary>
/// RN-03: la región se deriva de la modalidad. Onsite ⇒ el solicitante elige entre las regiones
/// presenciales; Online ⇒ región virtual forzada; Diploma ⇒ región de diplomado forzada. Una
/// combinación inválida se rechaza aquí, en el dominio — nunca solo en el navegador (hallazgo #2).
/// </summary>
public static class RegionAssignmentPolicy
{
    /// <summary>
    /// Resuelve la región efectiva para una modalidad. Para Onsite, <paramref name="requestedRegionId"/>
    /// es obligatorio (el solicitante elige entre las presenciales); para Online/Diploma se ignora
    /// cualquier región solicitada y se fuerza la única región activa de esa modalidad.
    /// </summary>
    public static Result<Region> Resolve(Modality modality, IReadOnlyList<Region> activeRegions, string? requestedRegionId)
    {
        var eligible = activeRegions.Where(r => r.Serves(modality)).ToList();

        if (eligible.Count == 0)
        {
            return Result.Failure<Region>(Error.Failure("RegionAssignmentPolicy.NoRegionConfigured", $"No hay ninguna región activa configurada para la modalidad '{modality}'."));
        }

        if (modality != Modality.Onsite)
        {
            // Online y Diploma están forzadas a una única región de su tipo.
            return Result.Success(eligible[0]);
        }

        if (string.IsNullOrWhiteSpace(requestedRegionId))
        {
            return Result.Failure<Region>(Error.Validation("RegionAssignmentPolicy.RegionRequired", "Debe elegir una región presencial."));
        }

        var chosen = eligible.FirstOrDefault(r => r.Id == requestedRegionId);
        return chosen is null
            ? Result.Failure<Region>(Error.Validation("RegionAssignmentPolicy.InvalidRegion", "La región elegida no es válida para la modalidad Presencial."))
            : Result.Success(chosen);
    }
}
