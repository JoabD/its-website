using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence;

/// <summary>
/// Generador de matrícula secuencial con findAndModify sobre 'counters' — sin condiciones de carrera
/// (spec técnico §3, Fase 3). El migrador (Fase 8) usa <see cref="EnsureSequenceAtLeastAsync"/> para
/// dejar el contador en MAX(USUARIOS.USUARIO) legado.
/// </summary>
public sealed class EnrollmentNumberGenerator(MongoContext context) : IEnrollmentNumberGenerator
{
    private const string CounterId = "enrollmentNumber";

    public async Task<EnrollmentNumber> NextAsync(CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", CounterId);
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        var doc = await context.Counters.FindOneAndUpdateAsync(filter, update, options, ct);
        return EnrollmentNumber.Create(doc["seq"].AsInt32).Value;
    }

    public async Task EnsureSequenceAtLeastAsync(int minimumValue, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", CounterId);
        var current = await context.Counters.Find(filter).FirstOrDefaultAsync(ct);
        var currentValue = current?["seq"].AsInt32 ?? 0;

        if (currentValue < minimumValue)
        {
            var update = Builders<BsonDocument>.Update.Set("seq", minimumValue);
            await context.Counters.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }
    }
}
