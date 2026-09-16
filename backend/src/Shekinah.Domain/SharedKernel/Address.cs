using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>Domicilio, tal como se captura tanto para el solicitante como para su iglesia.</summary>
public sealed record Address
{
    public string Street { get; }

    public string Neighborhood { get; }

    public string Locality { get; }

    public string Municipality { get; }

    public string? State { get; }

    private Address(string street, string neighborhood, string locality, string municipality, string? state)
    {
        Street = street;
        Neighborhood = neighborhood;
        Locality = locality;
        Municipality = municipality;
        State = state;
    }

    public static Result<Address> Create(string? street, string? neighborhood, string? locality, string? municipality, string? state = null)
    {
        if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(municipality))
        {
            return Result.Failure<Address>(Error.Validation("Address.Incomplete", "Domicilio y municipio son requeridos."));
        }

        return Result.Success(new Address(
            street.Trim(),
            (neighborhood ?? string.Empty).Trim(),
            (locality ?? string.Empty).Trim(),
            municipality.Trim(),
            string.IsNullOrWhiteSpace(state) ? null : state.Trim()));
    }
}
