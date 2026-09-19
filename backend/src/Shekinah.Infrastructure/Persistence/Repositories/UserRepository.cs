using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;
using Shekinah.Infrastructure.Persistence;

namespace Shekinah.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(MongoContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(string id, CancellationToken ct)
    {
        var doc = await context.Users.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<User?> GetByEnrollmentNumberAsync(EnrollmentNumber enrollmentNumber, CancellationToken ct)
    {
        var doc = await context.Users.Find(Builders<BsonDocument>.Filter.Eq("enrollmentNumber", enrollmentNumber.Value)).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var doc = await context.Users.Find(Builders<BsonDocument>.Filter.Eq("profile.email", email.ToLowerInvariant())).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<User>> GetActiveAdministratorsAsync(CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("role", nameof(UserRole.Administrator)) &
                     Builders<BsonDocument>.Filter.Eq("status", nameof(UserStatus.Active));
        var docs = await context.Users.Find(filter).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task<(IReadOnlyList<User> Items, long TotalCount)> SearchAsync(UserRole? role, string? regionId, string? searchText, int page, int pageSize, CancellationToken ct)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filter = builder.Empty;

        if (role is not null) filter &= builder.Eq("role", role.Value.ToString());
        if (!string.IsNullOrWhiteSpace(regionId)) filter &= builder.Eq("region.id", ObjectId.Parse(regionId));
        if (!string.IsNullOrWhiteSpace(searchText)) filter &= builder.Regex("profile.fullName", new BsonRegularExpression(searchText, "i"));

        var total = await context.Users.CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await context.Users.Find(filter).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (docs.Select(ToDomain).ToList(), total);
    }

    public async Task<IReadOnlyList<User>> GetActiveStudentsInRegionAsync(string regionId, TermNumber? currentTerm, CancellationToken ct)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filter = builder.Eq("role", nameof(UserRole.Student)) &
                     builder.Eq("status", nameof(UserStatus.Active));

        if (!string.IsNullOrWhiteSpace(regionId) && ObjectId.TryParse(regionId, out var oid))
        {
            filter &= builder.Eq("region.id", oid);
        }

        if (currentTerm is not null)
        {
            filter &= builder.Eq("academic.currentTerm", currentTerm.Value);
        }

        var docs = await context.Users.Find(filter).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task AddAsync(User user, CancellationToken ct) => await context.Users.InsertOneAsync(ToBson(user), cancellationToken: ct);

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(user.Id));
        await context.Users.ReplaceOneAsync(filter, ToBson(user), cancellationToken: ct);
    }

    public async Task<int> GetMaxEnrollmentNumberAsync(CancellationToken ct)
    {
        var doc = await context.Users.Find(Builders<BsonDocument>.Filter.Empty)
            .SortByDescending(d => d["enrollmentNumber"]).Limit(1).FirstOrDefaultAsync(ct);
        return doc?["enrollmentNumber"].AsInt32 ?? 0;
    }

    internal static BsonDocument ToBson(User user)
    {
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.Parse(user.Id),
            ["enrollmentNumber"] = user.EnrollmentNumber.Value,
            ["matricula"] = user.Matricula is null ? BsonNull.Value : user.Matricula,
            ["role"] = user.Role.ToString(),
            ["status"] = user.Status.ToString(),
            ["credentials"] = new BsonDocument
            {
                ["passwordHash"] = user.Credentials.PasswordHash,
                ["mustChangePassword"] = user.Credentials.MustChangePassword,
                ["passwordUpdatedAt"] = user.Credentials.PasswordUpdatedAtUtc,
                ["failedAttempts"] = user.Credentials.FailedAttempts,
                ["lockedUntil"] = user.Credentials.LockedUntilUtc.HasValue ? user.Credentials.LockedUntilUtc.Value : BsonNull.Value,
            },
            ["region"] = user.Region is null ? BsonNull.Value : new BsonDocument
            {
                ["id"] = ObjectId.Parse(user.Region.Id),
                ["code"] = user.Region.Code,
                ["name"] = user.Region.Name,
            },
            ["modality"] = user.Modality?.ToString() ?? (BsonValue)BsonNull.Value,
            ["academic"] = user.Academic is null ? BsonNull.Value : new BsonDocument
            {
                ["currentTerm"] = user.Academic.CurrentTerm.Value,
                ["plan"] = user.Academic.Plan.ToString(),
                ["isGraduated"] = user.Academic.IsGraduated,
                ["enrolledAt"] = user.Academic.EnrolledAtUtc,
                ["graduatedAt"] = user.Academic.GraduatedAtUtc.HasValue ? user.Academic.GraduatedAtUtc.Value : BsonNull.Value,
            },
            ["billing"] = new BsonDocument
            {
                ["noticeCount"] = user.Billing.NoticeCount,
                ["lastNoticeAt"] = user.Billing.LastNoticeAtUtc.HasValue ? user.Billing.LastNoticeAtUtc.Value : BsonNull.Value,
                ["blockedAt"] = user.Billing.BlockedAtUtc.HasValue ? user.Billing.BlockedAtUtc.Value : BsonNull.Value,
            },
            ["profile"] = new BsonDocument
            {
                ["fullName"] = user.Profile.FullName.FullName,
                ["email"] = user.Profile.Email.Value,
                ["phone"] = user.Profile.Phone.Value,
                ["birthDate"] = user.Profile.BirthDate.ToDateTime(TimeOnly.MinValue),
                ["maritalStatus"] = user.Profile.MaritalStatus,
                ["address"] = user.Profile.Address.ToBson(),
                ["church"] = user.Profile.Church.ToBson(),
                ["education"] = user.Profile.Education.ToBson(),
                ["theologicalBackground"] = user.Profile.TheologicalBackground,
                ["studyPurpose"] = user.Profile.StudyPurpose,
            },
            ["admissionApplicationId"] = user.AdmissionApplicationId is null ? BsonNull.Value : ObjectId.Parse(user.AdmissionApplicationId),
            ["lastLoginAt"] = user.LastLoginAtUtc.HasValue ? user.LastLoginAtUtc.Value : BsonNull.Value,
            ["createdAt"] = user.CreatedAtUtc,
            ["version"] = user.Version + 1,
        };

        return doc;
    }

    internal static User ToDomain(BsonDocument doc)
    {
        var credentialsDoc = doc["credentials"].AsBsonDocument;
        var credentials = RehydrateCredentials(credentialsDoc);

        RegionRef? region = null;
        if (doc.TryGetValue("region", out var regionValue) && !regionValue.IsBsonNull)
        {
            var r = regionValue.AsBsonDocument;
            region = new RegionRef(r["id"].AsObjectId.ToString(), r["code"].AsInt32, r["name"].AsString);
        }

        Modality? modality = doc.TryGetValue("modality", out var modalityValue) && !modalityValue.IsBsonNull
            ? Enum.Parse<Modality>(modalityValue.AsString) : null;

        AcademicState? academic = null;
        if (doc.TryGetValue("academic", out var academicValue) && !academicValue.IsBsonNull)
        {
            var a = academicValue.AsBsonDocument;
            var term = TermNumber.Create(a["currentTerm"].AsInt32).Value;
            // Compat: documentos creados antes de que existiera "plan" (toda solicitud aprobada
            // siempre fue y sigue siendo Cuatrimestral) se leen como Quarterly por defecto.
            var plan = a.TryGetValue("plan", out var planValue) && !planValue.IsBsonNull
                ? Enum.Parse<StudyPlan>(planValue.AsString) : StudyPlan.Quarterly;
            var graduatedAt = a.TryGetValue("graduatedAt", out var g) && !g.IsBsonNull ? g.ToUniversalTime() : (DateTime?)null;
            academic = AcademicState.Rehydrate(plan, term, a["enrolledAt"].ToUniversalTime(), graduatedAt);
        }

        var billingDoc = doc["billing"].AsBsonDocument;
        var billing = BillingState.Rehydrate(
            billingDoc["noticeCount"].AsInt32,
            billingDoc.TryGetValue("lastNoticeAt", out var ln) && !ln.IsBsonNull ? ln.ToUniversalTime() : null,
            billingDoc.TryGetValue("blockedAt", out var ba) && !ba.IsBsonNull ? ba.ToUniversalTime() : null);

        var profileDoc = doc["profile"].AsBsonDocument;
        var profile = PersonalProfile.Create(
            PersonName.Create(profileDoc["fullName"].AsString).Value,
            Email.Create(profileDoc["email"].AsString).Value,
            PhoneNumber.Create(profileDoc["phone"].AsString).Value,
            DateOnly.FromDateTime(profileDoc["birthDate"].ToUniversalTime()),
            profileDoc.GetValue("maritalStatus", "").AsString,
            BsonMappingExtensions.AddressFromBson(profileDoc["address"].AsBsonDocument),
            BsonMappingExtensions.ChurchFromBson(profileDoc["church"].AsBsonDocument),
            BsonMappingExtensions.EducationFromBson(profileDoc["education"].AsBsonDocument),
            profileDoc.GetValue("theologicalBackground", "").AsString,
            profileDoc.GetValue("studyPurpose", "").AsString).Value;

        return User.Rehydrate(
            doc["_id"].AsObjectId.ToString(),
            EnrollmentNumber.Create(doc["enrollmentNumber"].AsInt32).Value,
            Enum.Parse<UserRole>(doc["role"].AsString),
            Enum.Parse<UserStatus>(doc["status"].AsString),
            credentials, region, modality, academic, billing, profile,
            doc.TryGetValue("admissionApplicationId", out var appId) && !appId.IsBsonNull ? appId.AsObjectId.ToString() : null,
            doc.TryGetValue("lastLoginAt", out var lla) && !lla.IsBsonNull ? lla.ToUniversalTime() : null,
            doc["createdAt"].ToUniversalTime(),
            doc.GetValue("version", 1).AsInt32,
            doc.TryGetValue("matricula", out var mat) && !mat.IsBsonNull ? mat.AsString : null);
    }

    private static Credentials RehydrateCredentials(BsonDocument credentialsDoc) => Credentials.Rehydrate(
        credentialsDoc["passwordHash"].AsString,
        credentialsDoc.GetValue("mustChangePassword", false).AsBoolean,
        credentialsDoc["passwordUpdatedAt"].ToUniversalTime(),
        credentialsDoc.GetValue("failedAttempts", 0).AsInt32,
        credentialsDoc.TryGetValue("lockedUntil", out var lu) && !lu.IsBsonNull ? lu.ToUniversalTime() : null);

    // Mismo patrón que PaymentRepository.RegisterImportBatchAsync/CompleteImportBatchAsync/GetImportBatchAsync —
    // colección propia (studentImportBatches) para no mezclar lotes de alumnos con lotes de pagos.
    public async Task<StudentImportBatch> RegisterStudentImportBatchAsync(string fileName, string uploadedByUserId, int totalRows, CancellationToken ct)
    {
        var id = ObjectId.GenerateNewId();
        var doc = new BsonDocument
        {
            ["_id"] = id,
            ["fileName"] = fileName,
            ["uploadedBy"] = uploadedByUserId,
            ["uploadedAt"] = DateTime.UtcNow,
            ["totalRows"] = totalRows,
            ["importedRows"] = 0,
            ["status"] = nameof(ImportBatchStatus.Processing),
            ["errors"] = new BsonArray(),
        };
        await context.StudentImportBatches.InsertOneAsync(doc, cancellationToken: ct);
        return new StudentImportBatch(id.ToString(), fileName, uploadedByUserId, doc["uploadedAt"].ToUniversalTime(), totalRows, 0, ImportBatchStatus.Processing, []);
    }

    public async Task CompleteStudentImportBatchAsync(string batchId, int importedRows, IReadOnlyList<StudentImportRowError> errors, CancellationToken ct)
    {
        var update = Builders<BsonDocument>.Update
            .Set("importedRows", importedRows)
            .Set("status", nameof(ImportBatchStatus.Completed))
            .Set("errors", new BsonArray(errors.Select(e => new BsonDocument
            {
                ["rowNumber"] = e.RowNumber, ["code"] = e.Code, ["message"] = e.Message, ["rawValues"] = new BsonArray(e.RawValues),
            })));

        await context.StudentImportBatches.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(batchId)), update, cancellationToken: ct);
    }

    public async Task<StudentImportBatch?> GetStudentImportBatchAsync(string batchId, CancellationToken ct)
    {
        var doc = await context.StudentImportBatches.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(batchId))).FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        var errors = doc["errors"].AsBsonArray.Select(e => new StudentImportRowError(
            e["rowNumber"].AsInt32, e["code"].AsString, e["message"].AsString,
            e["rawValues"].AsBsonArray.Select(v => v.AsString).ToList())).ToList();

        return new StudentImportBatch(
            doc["_id"].AsObjectId.ToString(), doc["fileName"].AsString, doc["uploadedBy"].AsString, doc["uploadedAt"].ToUniversalTime(),
            doc["totalRows"].AsInt32, doc["importedRows"].AsInt32, Enum.Parse<ImportBatchStatus>(doc["status"].AsString), errors);
    }
}
