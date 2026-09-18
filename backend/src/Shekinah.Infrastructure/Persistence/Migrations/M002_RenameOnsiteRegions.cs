using MongoDB.Bson;
using MongoDB.Driver;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>
/// Las 2 sedes presenciales originales ("Región Centro"/"Región Norte", legado de
/// inscripcion.js) se renombran a las sedes reales del instituto, y se agrega una tercera —
/// pedido explícito del usuario tras probar el wizard de inscripción en vivo. El código legado
/// (<c>code</c>) de cada región no cambia (solo el nombre visible), así que cualquier solicitud
/// de admisión ya guardada con esa región sigue apuntando al mismo documento.
/// </summary>
public sealed class M002_RenameOnsiteRegions : IMongoMigration
{
    public int Version => 2;

    public string Description => "Renombra las regiones presenciales a San Miguel/Cuautla y agrega Región Morelia.";

    public async Task UpAsync(IMongoDatabase db, CancellationToken ct)
    {
        var regions = db.GetCollection<BsonDocument>("regions");

        await RenameByCodeAsync(regions, code: 1, newName: "Región San Miguel", ct);
        await RenameByCodeAsync(regions, code: 2, newName: "Región Cuautla", ct);

        var morelia = await regions.Find(Builders<BsonDocument>.Filter.Eq("name", "Región Morelia")).FirstOrDefaultAsync(ct);
        if (morelia is null)
        {
            var maxCode = await regions.Find(FilterDefinition<BsonDocument>.Empty)
                .SortByDescending(d => d["code"]).Limit(1).FirstOrDefaultAsync(ct);
            var nextCode = (maxCode?["code"].AsInt32 ?? 0) + 1;

            await regions.InsertOneAsync(new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["code"] = nextCode,
                ["name"] = "Región Morelia",
                ["modalityScope"] = new BsonArray(["Onsite"]),
                ["isActive"] = true,
                ["version"] = 1,
                ["createdAt"] = DateTime.UtcNow,
                ["updatedAt"] = DateTime.UtcNow,
            }, cancellationToken: ct);
        }
    }

    private static async Task RenameByCodeAsync(IMongoCollection<BsonDocument> regions, int code, string newName, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("code", code);
        var update = Builders<BsonDocument>.Update.Set("name", newName).Set("updatedAt", DateTime.UtcNow);
        await regions.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
