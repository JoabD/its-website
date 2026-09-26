using MongoDB.Bson;
using MongoDB.Driver;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>
/// Bug real (2026-09, reportado por el cliente al importar/dar de alta alumnos sin correo):
/// M001 crea el índice único de "profile.email" con
/// <c>PartialFilterExpression = Filter.Exists("profile.email")</c>, pero
/// <see cref="Repositories.UserRepository"/>.ToBson SIEMPRE deja la llave "email" en el documento
/// (con <see cref="BsonNull"/> cuando el alumno no tiene correo) en vez de omitirla — y $exists es
/// true para una llave presente aunque su valor sea null. Resultado: el índice "parcial" terminaba
/// cubriendo a TODOS los alumnos, con y sin correo, así que el segundo alumno sin correo que se
/// daba de alta (manual o por Excel) tronaba con
/// "E11000 duplicate key ... dup key: { profile.email: null }" — un 500 genérico para el usuario.
/// Este migration reemplaza el índice por uno cuyo filtro exige que el valor sea de tipo string (no
/// solo que la llave exista), que sí excluye correctamente los documentos con email null —
/// retroactivo para los alumnos que ya estaban en la base y para los que se den de alta después.
/// </summary>
public sealed class M005_FixNullEmailUniqueIndex : IMongoMigration
{
    public int Version => 5;

    public string Description => "Corrige el índice único de profile.email para que ignore también los documentos con email null explícito (no solo ausente).";

    public async Task UpAsync(IMongoDatabase db, CancellationToken ct)
    {
        var users = db.GetCollection<BsonDocument>("users");

        var existingIndexes = await (await users.Indexes.ListAsync(ct)).ToListAsync(ct);
        if (existingIndexes.Any(i => i["name"].AsString == "profile.email_1"))
        {
            await users.Indexes.DropOneAsync("profile.email_1", ct);
        }

        await users.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("profile.email"),
                new CreateIndexOptions<BsonDocument>
                {
                    Unique = true,
                    // A diferencia de Filter.Exists (que también hace match con profile.email:
                    // null), Filter.Type solo hace match cuando el valor es efectivamente un string
                    // — un alumno sin correo queda fuera del índice sin importar cuántos otros
                    // alumnos también estén sin correo.
                    PartialFilterExpression = Builders<BsonDocument>.Filter.Type("profile.email", BsonType.String),
                }),
            cancellationToken: ct);
    }
}
