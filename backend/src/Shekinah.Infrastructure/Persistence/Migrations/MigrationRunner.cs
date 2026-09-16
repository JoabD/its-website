using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>
/// Runner idempotente con colección _migrations y bloqueo por documento para evitar ejecución
/// concurrente al escalar (spec técnico §5.5). Se invoca una vez al arrancar Shekinah.Api.
/// </summary>
public sealed class MigrationRunner(MongoContext context, IEnumerable<IMongoMigration> migrations, ILogger<MigrationRunner> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var migration in migrations.OrderBy(m => m.Version))
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", migration.Version);
            var existing = await context.Migrations.Find(filter).FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                continue; // Idempotente: ya se aplicó.
            }

            // Bloqueo optimista por documento: solo un nodo gana el upsert en un escalado horizontal.
            var lockDoc = new BsonDocument { ["_id"] = migration.Version, ["description"] = migration.Description, ["startedAt"] = DateTime.UtcNow, ["status"] = "Running" };
            try
            {
                await context.Migrations.InsertOneAsync(lockDoc, cancellationToken: ct);
            }
            catch (MongoWriteException)
            {
                continue; // Otro nodo ya la está ejecutando.
            }

            logger.LogInformation("Aplicando migración {Version}: {Description}", migration.Version, migration.Description);
            await migration.UpAsync(context.Database, ct);

            var update = Builders<BsonDocument>.Update.Set("status", "Completed").Set("completedAt", DateTime.UtcNow);
            await context.Migrations.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
    }
}
