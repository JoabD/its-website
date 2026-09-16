using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

public sealed record MinistryRole
{
    public bool HasRole { get; }

    public string? RoleName { get; }

    private MinistryRole(bool hasRole, string? roleName)
    {
        HasRole = hasRole;
        RoleName = roleName;
    }

    public static Result<MinistryRole> Create(bool hasRole, string? roleName)
    {
        if (hasRole && string.IsNullOrWhiteSpace(roleName))
        {
            return Result.Failure<MinistryRole>(Error.Validation("MinistryRole.MissingName", "Debe indicar el cargo cuando aplica."));
        }

        return Result.Success(new MinistryRole(hasRole, hasRole ? roleName!.Trim() : null));
    }

    public static MinistryRole None => new(false, null);
}

/// <summary>Datos eclesiásticos del solicitante/alumno (sección "Datos eclesiásticos" del formulario).</summary>
public sealed record ChurchInfo
{
    public string Name { get; }

    public Address Address { get; }

    public string PastorName { get; }

    public string TimeAttending { get; }

    public MinistryRole MinistryRole { get; }

    private ChurchInfo(string name, Address address, string pastorName, string timeAttending, MinistryRole ministryRole)
    {
        Name = name;
        Address = address;
        PastorName = pastorName;
        TimeAttending = timeAttending;
        MinistryRole = ministryRole;
    }

    public static Result<ChurchInfo> Create(string? name, Address? address, string? pastorName, string? timeAttending, MinistryRole? ministryRole)
    {
        if (string.IsNullOrWhiteSpace(name) || address is null || string.IsNullOrWhiteSpace(pastorName))
        {
            return Result.Failure<ChurchInfo>(Error.Validation("ChurchInfo.Incomplete", "Nombre de iglesia, domicilio y pastor son requeridos."));
        }

        return Result.Success(new ChurchInfo(
            name.Trim(),
            address,
            pastorName.Trim(),
            (timeAttending ?? string.Empty).Trim(),
            ministryRole ?? MinistryRole.None));
    }
}
