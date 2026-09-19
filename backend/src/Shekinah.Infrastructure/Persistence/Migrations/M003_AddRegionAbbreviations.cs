using MongoDB.Bson;
using MongoDB.Driver;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>
/// Agrega <c>abbreviation</c> a las regiones existentes — se usa para armar la matrícula de
/// alumnos aprobados (formato ITS/{Abbreviation}/{consecutivo}, ver <c>IMatriculaGenerator</c>).
/// Idempotente: solo toca documentos sin el campo o con él vacío, así que nunca pisa una
/// abreviatura que un administrador ya haya editado a mano desde el catálogo.
/// </summary>
public sealed class M003_AddRegionAbbreviations : IMongoMigration
{
    public int Version => 3;

    public string Description => "Agrega abreviaturas (SM/CU/MO/VI/DI) a las regiones existentes para la matrícula.";

    private static readonly Dictionary<string, string> AbbreviationsByName = new()
    {
        ["Región San Miguel"] = "SM",
        ["Región Cuautla"] = "CU",
        ["Región Morelia"] = "MO",
        ["Región Virtual"] = "VI",
        ["Región Diplomado"] = "DI",
    };

    public async Task UpAsync(IMongoDatabase db, CancellationToken ct)
    {
        var regions = db.GetCollection<BsonDocument>("regions");

        foreach (var (name, abbreviation) in AbbreviationsByName)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("name", name) &
                         (Builders<BsonDocument>.Filter.Exists("abbreviation", false) |
                          Builders<BsonDocument>.Filter.Eq("abbreviation", ""));
            var update = Builders<BsonDocument>.Update.Set("abbreviation", abbreviation).Set("updatedAt", DateTime.UtcNow);
            await regions.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
    }
}
