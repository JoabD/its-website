using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Admissions;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>
/// Siembra el catálogo editable de documentos de inscripción (Configuración → Documentos de
/// inscripción) con los 4 ítems que hasta ahora eran fijos en el dominio. Idempotente: usa upsert
/// por _id, así que si el administrador ya renombró/desactivó alguno, correr esto de nuevo no lo
/// pisa (solo el <c>displayOrder</c> inicial, que además solo aplica si el documento es nuevo).
/// </summary>
public sealed class M004_SeedChecklistItemDefinitions : IMongoMigration
{
    public int Version => 4;

    public string Description => "Siembra el catálogo de documentos de inscripción (checklist) con los 4 ítems iniciales.";

    public async Task UpAsync(IMongoDatabase db, CancellationToken ct)
    {
        var collection = db.GetCollection<BsonDocument>("checklistItemDefinitions");

        foreach (var (id, label, displayOrder) in ChecklistItemDefinitionSeed.Defaults)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
            var existing = await collection.Find(filter).FirstOrDefaultAsync(ct);
            if (existing is not null) continue;

            var doc = new BsonDocument
            {
                ["_id"] = id,
                ["label"] = label,
                ["displayOrder"] = displayOrder,
                ["isActive"] = true,
                ["createdAt"] = DateTime.UtcNow,
                ["updatedAt"] = DateTime.UtcNow,
            };
            await collection.InsertOneAsync(doc, cancellationToken: ct);
        }
    }
}
