using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Admissions;

namespace Shekinah.Infrastructure.Persistence.Repositories;

/// <summary>
/// A diferencia de la mayoría de los repositorios del proyecto, el _id aquí es un slug de texto
/// legible (ej. "official-id"), no un ObjectId — igual que la colección `counters`. Son solo un
/// puñado de documentos de catálogo, editados a mano desde Configuración, así que un id legible
/// ayuda a depurar directamente en Mongo sin tener que resolver un ObjectId contra un nombre.
/// </summary>
public sealed class ChecklistItemDefinitionRepository(MongoContext context) : IChecklistItemDefinitionRepository
{
    public async Task<ChecklistItemDefinition?> GetByIdAsync(string id, CancellationToken ct)
    {
        var doc = await context.ChecklistItemDefinitions.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<ChecklistItemDefinition>> GetAllAsync(CancellationToken ct)
    {
        var docs = await context.ChecklistItemDefinitions.Find(Builders<BsonDocument>.Filter.Empty)
            .SortBy(d => d["displayOrder"]).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChecklistItemDefinition>> GetActiveAsync(CancellationToken ct)
    {
        var docs = await context.ChecklistItemDefinitions.Find(Builders<BsonDocument>.Filter.Eq("isActive", true))
            .SortBy(d => d["displayOrder"]).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task AddAsync(ChecklistItemDefinition item, CancellationToken ct) =>
        await context.ChecklistItemDefinitions.InsertOneAsync(ToBson(item), cancellationToken: ct);

    public async Task UpdateAsync(ChecklistItemDefinition item, CancellationToken ct) =>
        await context.ChecklistItemDefinitions.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", item.Id), ToBson(item), cancellationToken: ct);

    internal static BsonDocument ToBson(ChecklistItemDefinition item) => new()
    {
        ["_id"] = item.Id,
        ["label"] = item.Label,
        ["displayOrder"] = item.DisplayOrder,
        ["isActive"] = item.IsActive,
        ["updatedAt"] = DateTime.UtcNow,
    };

    internal static ChecklistItemDefinition ToDomain(BsonDocument doc) => ChecklistItemDefinition.Rehydrate(
        doc["_id"].AsString, doc["label"].AsString, doc.GetValue("displayOrder", 0).AsInt32, doc.GetValue("isActive", true).AsBoolean);
}
