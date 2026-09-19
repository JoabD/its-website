using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Persistence;

/// <summary>
/// Matrícula ITS/{Abreviatura}/{consecutivo con padding a 5 dígitos} — un contador atómico
/// findAndModify por región (mismo patrón que <see cref="EnrollmentNumberGenerator"/>, solo que
/// con un _id de contador distinto por cada abreviatura, así cada sede tiene su propia numeración
/// empezando en 1).
/// </summary>
public sealed class MatriculaGenerator(MongoContext context) : IMatriculaGenerator
{
    public async Task<string> NextAsync(string regionAbbreviation, CancellationToken ct)
    {
        var abbreviation = string.IsNullOrWhiteSpace(regionAbbreviation) ? "ITS" : regionAbbreviation.Trim().ToUpperInvariant();
        var filter = Builders<BsonDocument>.Filter.Eq("_id", $"matricula:{abbreviation}");
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        var doc = await context.Counters.FindOneAndUpdateAsync(filter, update, options, ct);
        var sequence = doc["seq"].AsInt32;

        return $"ITS/{abbreviation}/{sequence:D5}";
    }
}
