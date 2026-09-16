using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Admissions;

/// <summary>
/// Elección de modalidad + región derivada (RN-02, RN-03). La coherencia se valida aquí, en el
/// dominio, no en el navegador (hallazgo de auditoría #2: "el backend no valida").
/// </summary>
public sealed record ModalityChoice
{
    public Modality Modality { get; }

    public string RegionId { get; }

    public int RegionCode { get; }

    public string RegionName { get; }

    /// <summary>Requerido y obligatorio solo cuando <see cref="Modality"/> es <see cref="Modality.Online"/>.</summary>
    public string? OnlineReason { get; }

    private ModalityChoice(Modality modality, string regionId, int regionCode, string regionName, string? onlineReason)
    {
        Modality = modality;
        RegionId = regionId;
        RegionCode = regionCode;
        RegionName = regionName;
        OnlineReason = onlineReason;
    }

    /// <summary>
    /// Único factory: recibe la región ya resuelta por <c>RegionAssignmentPolicy</c> (Domain Service)
    /// para no duplicar la regla "modalidad → región" en dos sitios.
    /// </summary>
    public static Result<ModalityChoice> Create(Modality modality, Region region, string? onlineReason)
    {
        if (!region.Serves(modality))
        {
            return Result.Failure<ModalityChoice>(Error.Validation(
                "ModalityChoice.RegionMismatch",
                $"La región '{region.Name}' no atiende la modalidad '{modality}'."));
        }

        if (modality == Modality.Online && string.IsNullOrWhiteSpace(onlineReason))
        {
            return Result.Failure<ModalityChoice>(Error.Validation(
                "ModalityChoice.OnlineReasonRequired",
                "La modalidad Virtual requiere especificar el motivo."));
        }

        return Result.Success(new ModalityChoice(
            modality, region.Id, region.LegacyCode, region.Name,
            modality == Modality.Online ? onlineReason!.Trim() : null));
    }
}
