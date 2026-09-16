using MongoDB.Driver;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>Migrador de esquema propio, versionado e idempotente (spec técnico §5.5).</summary>
public interface IMongoMigration
{
    int Version { get; }

    string Description { get; }

    Task UpAsync(IMongoDatabase db, CancellationToken ct);
}
