using MongoDB.Bson;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence;

/// <summary>
/// Mapeo manual y explícito BsonDocument ⇄ Value Object. Los agregados del dominio no exponen
/// constructores públicos ni setters (invariantes protegidos, spec técnico §3.3), así que en lugar
/// de forzar un BsonClassMap reflexivo sobre ellos, cada repositorio construye/lee BsonDocument
/// "a mano" a través de estos helpers. Es más código, pero el dominio permanece 100% ignorante de
/// Mongo (ni un solo atributo Bson en Shekinah.Domain).
/// </summary>
public static class BsonMappingExtensions
{
    public static BsonDocument ToBson(this Address address) => new()
    {
        ["street"] = address.Street,
        ["neighborhood"] = address.Neighborhood,
        ["locality"] = address.Locality,
        ["municipality"] = address.Municipality,
        ["state"] = address.State is null ? BsonNull.Value : address.State,
    };

    public static Address AddressFromBson(BsonDocument doc)
    {
        var street = doc.GetValue("street", "").AsString;
        var neighborhood = doc.GetValue("neighborhood", "").AsString;
        var locality = doc.GetValue("locality", "").AsString;
        var municipality = doc.GetValue("municipality", "").AsString;
        var state = doc.TryGetValue("state", out var stateValue) && !stateValue.IsBsonNull ? stateValue.AsString : null;
        return string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(municipality)
            ? Address.CreateOptional(street, neighborhood, locality, municipality, state).Value
            : Address.Create(street, neighborhood, locality, municipality, state).Value;
    }

    public static BsonDocument ToBson(this ChurchInfo church) => new()
    {
        ["name"] = church.Name,
        ["address"] = church.Address.ToBson(),
        ["pastorName"] = church.PastorName,
        ["timeAttending"] = church.TimeAttending,
        ["ministryRole"] = new BsonDocument
        {
            ["hasRole"] = church.MinistryRole.HasRole,
            ["roleName"] = church.MinistryRole.RoleName is null ? BsonNull.Value : church.MinistryRole.RoleName,
        },
    };

    public static ChurchInfo ChurchFromBson(BsonDocument doc)
    {
        var ministryDoc = doc["ministryRole"].AsBsonDocument;
        var hasRole = ministryDoc.GetValue("hasRole", false).AsBoolean;
        var roleName = ministryDoc.TryGetValue("roleName", out var rn) && !rn.IsBsonNull ? rn.AsString : null;
        var ministryRole = MinistryRole.Create(hasRole, roleName).Value;

        var name = doc.GetValue("name", "").AsString;
        var address = AddressFromBson(doc["address"].AsBsonDocument);
        var pastorName = doc.GetValue("pastorName", "").AsString;
        var timeAttending = doc.GetValue("timeAttending", "").AsString;
        return string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(pastorName)
            ? ChurchInfo.CreateForInitialApplication(name, address, pastorName, timeAttending, ministryRole).Value
            : ChurchInfo.Create(name, address, pastorName, timeAttending, ministryRole).Value;
    }

    public static BsonDocument ToBson(this EducationLevel education) => new()
    {
        ["level"] = education.Level.ToString(),
        ["otherDescription"] = education.OtherDescription is null ? BsonNull.Value : education.OtherDescription,
    };

    public static EducationLevel EducationFromBson(BsonDocument doc) => EducationLevel.Create(
        Enum.Parse<SchoolingLevel>(doc.GetValue("level", "Other").AsString),
        doc.TryGetValue("otherDescription", out var o) && !o.IsBsonNull ? o.AsString : null).Value;

    public static BsonDocument? ToBson(this Money? money) => money is null ? null : new BsonDocument
    {
        ["amount"] = money.Amount,
        ["currency"] = money.Currency,
    };

    public static Money? MoneyFromBson(BsonValue? value) =>
        value is null || value.IsBsonNull ? null : Money.Create(value.AsBsonDocument["amount"].ToDecimal(), value.AsBsonDocument["currency"].AsString).Value;

    public static string NewId() => ObjectIdOrGuid();

    private static string ObjectIdOrGuid() => ObjectId.GenerateNewId().ToString();
}
