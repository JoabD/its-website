using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>
/// Da de alta el currículo del plan Semestral (Presencial) — 26 materias en 6 semestres, tomadas
/// literalmente del certificado "FORMACION TEOLOGICA INTEGRAL MODALIDAD PRESENCIAL DE ENERO 2023 A
/// ENERO 2026" — sin tocar el plan Cuatrimestral (Virtual) que ya existía. Se ejecuta como migración
/// (no como parte de <c>DatabaseSeeder</c>) porque el seeder de materias solo corre en una base
/// totalmente vacía, y la base real ya tiene las 28 materias cuatrimestrales sembradas (plan de
/// control escolar / Kardex, fase 1). Idempotente por código de materia.
/// </summary>
public sealed class M006_SeedSemesterPlanSubjects : IMongoMigration
{
    public int Version => 6;

    public string Description => "Siembra las 26 materias del plan Semestral (Presencial) en 6 semestres.";

    private static readonly (int Term, string[] Names)[] Curriculum =
    [
        (1, ["Pentateuco", "Introducción a la Teología", "Métodos de Investigación", "Introducción Bíblica"]),
        (2, ["Libros Históricos 1", "Pneumatología", "Historia Eclesiástica 1", "Homilética", "Teología de la Educación Cristiana"]),
        (3, ["Libros Históricos 2", "Evangelios Sinópticos", "Hechos de los Apóstoles", "Historia Eclesiástica 2"]),
        (4, ["Liderazgo Integral", "Libros Sapienciales", "Hermenéutica", "Eclesiología"]),
        (5, ["Teología Propia", "Profetas Mayores", "Sermón Expositivo", "Griego 1", "Gramática Española"]),
        (6, ["Griego 2", "Periodo Intertestamentario", "Consejería Pastoral", "Apologética"]),
    ];

    public async Task UpAsync(IMongoDatabase db, CancellationToken ct)
    {
        var collection = db.GetCollection<BsonDocument>("subjects");
        var order = 1;

        foreach (var (term, names) in Curriculum)
        {
            foreach (var name in names)
            {
                var code = $"S{term}-{order:D2}";
                var exists = await collection.Find(Builders<BsonDocument>.Filter.Eq("code", code)).AnyAsync(ct);
                if (!exists)
                {
                    var doc = new BsonDocument
                    {
                        ["_id"] = ObjectId.GenerateNewId(),
                        ["code"] = code,
                        ["legacyId"] = BsonNull.Value,
                        ["name"] = name,
                        ["programType"] = ProgramType.Quarterly.ToString(),
                        ["termNumber"] = term,
                        ["displayOrder"] = order,
                        ["plan"] = StudyPlan.Semester.ToString(),
                        ["syllabus"] = new BsonDocument { ["pdfUrl"] = BsonNull.Value, ["publishedAt"] = BsonNull.Value },
                        ["isActive"] = true,
                        ["version"] = 1,
                        ["createdAt"] = DateTime.UtcNow,
                        ["updatedAt"] = DateTime.UtcNow,
                    };
                    await collection.InsertOneAsync(doc, cancellationToken: ct);
                }

                order++;
            }
        }
    }
}
