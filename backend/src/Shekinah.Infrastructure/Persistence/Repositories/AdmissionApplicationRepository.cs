using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence.Repositories;

public sealed class AdmissionApplicationRepository(MongoContext context) : IAdmissionApplicationRepository
{
    public async Task<AdmissionApplication?> GetByIdAsync(string id, CancellationToken ct)
    {
        var doc = await context.AdmissionApplications.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<AdmissionApplication?> GetByFolioAsync(string folio, CancellationToken ct)
    {
        var doc = await context.AdmissionApplications.Find(Builders<BsonDocument>.Filter.Eq("folio", folio)).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<(IReadOnlyList<AdmissionApplication> Items, long TotalCount)> SearchAsync(ApplicationStatus? status, string? searchText, int page, int pageSize, CancellationToken ct)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filter = builder.Empty;
        if (status is not null) filter &= builder.Eq("status", status.Value.ToString());
        if (!string.IsNullOrWhiteSpace(searchText)) filter &= builder.Regex("applicant.fullName", new BsonRegularExpression(searchText, "i"));

        var total = await context.AdmissionApplications.CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await context.AdmissionApplications.Find(filter).SortByDescending(d => d["submittedAt"])
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (docs.Select(ToDomain).ToList(), total);
    }

    public async Task AddAsync(AdmissionApplication application, CancellationToken ct) =>
        await context.AdmissionApplications.InsertOneAsync(ToBson(application), cancellationToken: ct);

    public async Task UpdateAsync(AdmissionApplication application, CancellationToken ct) =>
        await context.AdmissionApplications.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(application.Id)), ToBson(application), cancellationToken: ct);

    public async Task<int> GetNextLegacySequenceAsync(CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", "admissionApplicationFolio");
        var update = Builders<BsonDocument>.Update.Inc("seq", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        var doc = await context.Counters.FindOneAndUpdateAsync(filter, update, options, ct);
        return doc["seq"].AsInt32;
    }

    internal static BsonDocument ToBson(AdmissionApplication app) => new()
    {
        ["_id"] = ObjectId.Parse(app.Id),
        ["folio"] = app.Folio,
        ["legacyId"] = app.LegacyId.HasValue ? app.LegacyId.Value : BsonNull.Value,
        ["applicant"] = new BsonDocument
        {
            ["fullName"] = app.Applicant.FullName.FullName,
            ["birthDate"] = app.Applicant.BirthDate.ToDateTime(TimeOnly.MinValue),
            ["maritalStatus"] = app.Applicant.MaritalStatus,
            ["email"] = app.Applicant.Email.Value,
            ["phone"] = app.Applicant.Phone.Value,
            ["address"] = app.Applicant.Address.ToBson(),
            ["church"] = app.Applicant.Church.ToBson(),
            ["education"] = app.Applicant.Education.ToBson(),
            ["theologicalBackground"] = app.Applicant.TheologicalBackground,
            ["studyPurpose"] = app.Applicant.StudyPurpose,
        },
        ["modality"] = app.ModalityChoice.Modality.ToString(),
        ["onlineReason"] = app.ModalityChoice.OnlineReason is null ? BsonNull.Value : app.ModalityChoice.OnlineReason,
        ["requestedRegion"] = new BsonDocument
        {
            ["id"] = ObjectId.Parse(app.ModalityChoice.RegionId),
            ["code"] = app.ModalityChoice.RegionCode,
            ["name"] = app.ModalityChoice.RegionName,
        },
        ["documents"] = new BsonArray(app.Documents.Select(d => new BsonDocument
        {
            ["fileId"] = d.FileId, ["fileName"] = d.FileName, ["contentType"] = d.ContentType,
            ["sizeBytes"] = d.SizeBytes, ["uploadedAt"] = d.UploadedAtUtc,
        })),
        ["status"] = app.Status.ToString(),
        ["submittedAt"] = app.SubmittedAtUtc,
        ["decision"] = app.Decision is null ? BsonNull.Value : new BsonDocument
        {
            ["decidedAt"] = app.Decision.DecidedAtUtc,
            ["decidedBy"] = app.Decision.DecidedByUserId,
            ["decidedByName"] = app.Decision.DecidedByName,
            ["reason"] = app.Decision.Reason is null ? BsonNull.Value : app.Decision.Reason,
        },
        ["createdUserId"] = app.CreatedUserId is null ? BsonNull.Value : ObjectId.Parse(app.CreatedUserId),
        ["version"] = 1,
        ["createdAt"] = app.SubmittedAtUtc,
        ["updatedAt"] = DateTime.UtcNow,
    };

    internal static AdmissionApplication ToDomain(BsonDocument doc)
    {
        var applicantDoc = doc["applicant"].AsBsonDocument;
        var applicant = ApplicantProfile.Create(
            PersonName.Create(applicantDoc["fullName"].AsString).Value,
            DateOnly.FromDateTime(applicantDoc["birthDate"].ToUniversalTime()),
            applicantDoc.GetValue("maritalStatus", "").AsString,
            Email.Create(applicantDoc["email"].AsString).Value,
            PhoneNumber.Create(applicantDoc["phone"].AsString).Value,
            BsonMappingExtensions.AddressFromBson(applicantDoc["address"].AsBsonDocument),
            BsonMappingExtensions.ChurchFromBson(applicantDoc["church"].AsBsonDocument),
            BsonMappingExtensions.EducationFromBson(applicantDoc["education"].AsBsonDocument),
            applicantDoc.GetValue("theologicalBackground", "").AsString,
            applicantDoc.GetValue("studyPurpose", "").AsString).Value;

        var regionDoc = doc["requestedRegion"].AsBsonDocument;
        var region = Domain.Catalog.Region.Create(
            regionDoc["id"].AsObjectId.ToString(), regionDoc["code"].AsInt32, regionDoc["name"].AsString,
            [Enum.Parse<Modality>(doc["modality"].AsString)]).Value;

        var modalityChoice = ModalityChoice.Create(
            Enum.Parse<Modality>(doc["modality"].AsString), region,
            doc.TryGetValue("onlineReason", out var or) && !or.IsBsonNull ? or.AsString : null).Value;

        var decisionDoc = doc.TryGetValue("decision", out var d) && !d.IsBsonNull ? d.AsBsonDocument : null;
        ApplicationDecision? decision = decisionDoc is null ? null : new ApplicationDecision(
            decisionDoc["decidedAt"].ToUniversalTime(), decisionDoc["decidedBy"].AsString, decisionDoc["decidedByName"].AsString,
            decisionDoc.TryGetValue("reason", out var r) && !r.IsBsonNull ? r.AsString : null);

        return AdmissionApplication.Rehydrate(
            doc["_id"].AsObjectId.ToString(), doc["folio"].AsString,
            doc.TryGetValue("legacyId", out var lid) && !lid.IsBsonNull ? lid.AsInt32 : 0,
            applicant, modalityChoice, Enum.Parse<ApplicationStatus>(doc["status"].AsString),
            doc["submittedAt"].ToUniversalTime(), decision,
            doc.TryGetValue("createdUserId", out var cu) && !cu.IsBsonNull ? cu.AsObjectId.ToString() : null);
    }
}
