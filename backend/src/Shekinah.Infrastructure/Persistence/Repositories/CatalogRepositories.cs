using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence.Repositories;

public sealed class RegionRepository(MongoContext context) : IRegionRepository
{
    public async Task<Region?> GetByIdAsync(string id, CancellationToken ct)
    {
        var doc = await context.Regions.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<Region>> GetActiveAsync(CancellationToken ct)
    {
        var docs = await context.Regions.Find(Builders<BsonDocument>.Filter.Eq("isActive", true)).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Region>> GetActiveByModalityAsync(Modality modality, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("isActive", true) &
                     Builders<BsonDocument>.Filter.AnyEq("modalityScope", modality.ToString());
        var docs = await context.Regions.Find(filter).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task AddAsync(Region region, CancellationToken ct) => await context.Regions.InsertOneAsync(ToBson(region), cancellationToken: ct);

    public async Task UpdateAsync(Region region, CancellationToken ct) =>
        await context.Regions.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(region.Id)), ToBson(region), cancellationToken: ct);

    internal static BsonDocument ToBson(Region region) => new()
    {
        ["_id"] = ObjectId.Parse(region.Id),
        ["code"] = region.LegacyCode,
        ["name"] = region.Name,
        ["abbreviation"] = region.Abbreviation,
        ["modalityScope"] = new BsonArray(region.ModalityScope.Select(m => m.ToString())),
        ["isActive"] = region.IsActive,
        ["version"] = 1,
        ["createdAt"] = DateTime.UtcNow,
        ["updatedAt"] = DateTime.UtcNow,
    };

    internal static Region ToDomain(BsonDocument doc)
    {
        // Defensivo (bug real 2026-09: 500 al "Descargar plantilla" — la primera función que carga
        // TODAS las regiones activas de un jalón y lee modalityScope de cada una): algunas regiones
        // existentes (aparentemente creadas/editadas a mano en Mongo antes de que este campo
        // existiera — ej. las que el cliente reportó no saber cómo quedaron internamente) no tienen
        // "modalityScope" guardado, y el indexador de BsonDocument truena si la llave no existe. Si
        // falta, se asume que la región sirve las tres modalidades (comportamiento histórico antes de
        // que existiera esta restricción), en vez de tumbar toda la petición.
        var scope = doc.TryGetValue("modalityScope", out var scopeValue) && !scopeValue.IsBsonNull && scopeValue.AsBsonArray.Count > 0
            ? scopeValue.AsBsonArray.Select(v => Enum.Parse<Modality>(v.AsString))
            : Enum.GetValues<Modality>();
        var abbreviation = doc.TryGetValue("abbreviation", out var abbr) && !abbr.IsBsonNull ? abbr.AsString : null;
        var region = Region.Create(doc["_id"].AsObjectId.ToString(), doc["code"].AsInt32, doc["name"].AsString, scope, abbreviation).Value;
        if (!doc.GetValue("isActive", true).AsBoolean) region.Deactivate();
        return region;
    }
}

public sealed class SubjectRepository(MongoContext context) : ISubjectRepository
{
    public async Task<Subject?> GetByIdAsync(string id, CancellationToken ct)
    {
        var doc = await context.Subjects.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<Subject?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var doc = await context.Subjects.Find(Builders<BsonDocument>.Filter.Eq("code", code)).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<Subject>> GetCurriculumAsync(CancellationToken ct)
    {
        var docs = await context.Subjects.Find(Builders<BsonDocument>.Filter.Eq("isActive", true)).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Subject>> GetByTermAsync(TermNumber termNumber, CancellationToken ct)
    {
        var docs = await context.Subjects.Find(Builders<BsonDocument>.Filter.Eq("termNumber", termNumber.Value)).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task AddAsync(Subject subject, CancellationToken ct) => await context.Subjects.InsertOneAsync(ToBson(subject), cancellationToken: ct);

    public async Task UpdateAsync(Subject subject, CancellationToken ct) =>
        await context.Subjects.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(subject.Id)), ToBson(subject), cancellationToken: ct);

    public async Task DeleteAsync(string id, CancellationToken ct) =>
        await context.Subjects.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id)), ct);

    internal static BsonDocument ToBson(Subject subject) => new()
    {
        ["_id"] = ObjectId.Parse(subject.Id),
        ["code"] = subject.Code,
        ["legacyId"] = subject.LegacyId.HasValue ? subject.LegacyId.Value : BsonNull.Value,
        ["name"] = subject.Name,
        ["programType"] = subject.ProgramType.ToString(),
        ["termNumber"] = subject.TermNumber?.Value ?? (BsonValue)BsonNull.Value,
        ["displayOrder"] = subject.DisplayOrder,
        ["syllabus"] = new BsonDocument
        {
            ["pdfUrl"] = subject.Syllabus.PdfUrl is null ? BsonNull.Value : subject.Syllabus.PdfUrl,
            ["publishedAt"] = subject.Syllabus.PublishedAtUtc.HasValue ? subject.Syllabus.PublishedAtUtc.Value : BsonNull.Value,
        },
        ["isActive"] = subject.IsActive,
        ["version"] = 1,
        ["createdAt"] = DateTime.UtcNow,
        ["updatedAt"] = DateTime.UtcNow,
    };

    internal static Subject ToDomain(BsonDocument doc)
    {
        var programType = Enum.Parse<ProgramType>(doc["programType"].AsString);
        var legacyId = doc.TryGetValue("legacyId", out var lid) && !lid.IsBsonNull ? lid.AsInt32 : (int?)null;

        var subject = programType == ProgramType.Diploma
            ? Subject.CreateDiploma(doc["_id"].AsObjectId.ToString(), doc["code"].AsString, doc["name"].AsString, doc["displayOrder"].AsInt32, legacyId).Value
            : Subject.CreateQuarterly(doc["_id"].AsObjectId.ToString(), doc["code"].AsString, doc["name"].AsString,
                TermNumber.Create(doc["termNumber"].AsInt32).Value, doc["displayOrder"].AsInt32, legacyId).Value;

        var syllabusDoc = doc["syllabus"].AsBsonDocument;
        if (syllabusDoc.TryGetValue("pdfUrl", out var pdf) && !pdf.IsBsonNull)
        {
            subject.PublishSyllabus(pdf.AsString, new Time.SystemClock());
        }

        return subject;
    }
}
