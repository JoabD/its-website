using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Academics;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence.Repositories;

public sealed class AcademicPeriodRepository(MongoContext context) : IAcademicPeriodRepository
{
    public async Task<AcademicPeriod?> GetByIdAsync(string id, CancellationToken ct)
    {
        var doc = await context.AcademicPeriods.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    /// <summary>RN-13: a lo sumo un periodo Active. Reforzado también por índice único parcial (spec técnico §5.3).</summary>
    public async Task<AcademicPeriod?> GetActiveAsync(CancellationToken ct)
    {
        var doc = await context.AcademicPeriods.Find(Builders<BsonDocument>.Filter.Eq("status", nameof(AcademicPeriodStatus.Active))).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<(IReadOnlyList<AcademicPeriod> Items, long TotalCount)> SearchAsync(int page, int pageSize, CancellationToken ct)
    {
        var total = await context.AcademicPeriods.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken: ct);
        var docs = await context.AcademicPeriods.Find(Builders<BsonDocument>.Filter.Empty)
            .SortByDescending(d => d["openedAt"]).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (docs.Select(ToDomain).ToList(), total);
    }

    public async Task AddAsync(AcademicPeriod period, CancellationToken ct) => await context.AcademicPeriods.InsertOneAsync(ToBson(period), cancellationToken: ct);

    public async Task UpdateAsync(AcademicPeriod period, CancellationToken ct) =>
        await context.AcademicPeriods.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(period.Id)), ToBson(period), cancellationToken: ct);

    internal static BsonDocument ToBson(AcademicPeriod period) => new()
    {
        ["_id"] = ObjectId.Parse(period.Id),
        ["code"] = period.Code.Value,
        ["name"] = period.Name,
        ["startsOn"] = period.DateRange.StartsOnUtc,
        ["endsOn"] = period.DateRange.EndsOnUtc,
        ["status"] = period.Status.ToString(),
        ["monthCodes"] = new BsonArray(period.MonthCodes.Select(m => m.Value)),
        ["openedAt"] = period.OpenedAtUtc,
        ["openedBy"] = period.OpenedByUserId,
        ["closedAt"] = period.ClosedAtUtc.HasValue ? period.ClosedAtUtc.Value : BsonNull.Value,
        ["closedBy"] = period.ClosedByUserId is null ? BsonNull.Value : period.ClosedByUserId,
        ["version"] = 1,
        ["createdAt"] = period.OpenedAtUtc,
        ["updatedAt"] = DateTime.UtcNow,
    };

    internal static AcademicPeriod ToDomain(BsonDocument doc) => AcademicPeriod.Rehydrate(
        doc["_id"].AsObjectId.ToString(),
        PeriodCode.Create(doc["code"].AsString).Value,
        doc["name"].AsString,
        DateRange.Create(doc["startsOn"].ToUniversalTime(), doc["endsOn"].ToUniversalTime()).Value,
        doc["monthCodes"].AsBsonArray.Select(v => MonthCode.Create(v.AsString).Value).ToList(),
        Enum.Parse<AcademicPeriodStatus>(doc["status"].AsString),
        doc["openedAt"].ToUniversalTime(),
        doc["openedBy"].AsString,
        doc.TryGetValue("closedAt", out var ca) && !ca.IsBsonNull ? ca.ToUniversalTime() : null,
        doc.TryGetValue("closedBy", out var cb) && !cb.IsBsonNull ? cb.AsString : null);
}

public sealed class CourseOfferingRepository(MongoContext context) : ICourseOfferingRepository
{
    public async Task<CourseOffering?> GetByIdAsync(string id, CancellationToken ct)
    {
        var doc = await context.CourseOfferings.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    /// <summary>RN-22: único por (periodo, materia, región).</summary>
    public async Task<CourseOffering?> FindAsync(string periodId, string subjectId, string regionId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("period.id", ObjectId.Parse(periodId)) &
                     Builders<BsonDocument>.Filter.Eq("subject.id", ObjectId.Parse(subjectId)) &
                     Builders<BsonDocument>.Filter.Eq("region.id", ObjectId.Parse(regionId));
        var doc = await context.CourseOfferings.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<CourseOffering>> GetByPeriodAsync(string periodId, CancellationToken ct)
    {
        var docs = await context.CourseOfferings.Find(Builders<BsonDocument>.Filter.Eq("period.id", ObjectId.Parse(periodId))).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<CourseOffering>> GetByTeacherAndPeriodAsync(string teacherId, string periodId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("teacher.id", ObjectId.Parse(teacherId)) &
                     Builders<BsonDocument>.Filter.Eq("period.id", ObjectId.Parse(periodId));
        var docs = await context.CourseOfferings.Find(filter).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<CourseOffering>> GetByStudentAndPeriodAsync(string studentId, string periodId, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("enrollments.studentId", ObjectId.Parse(studentId)) &
                     Builders<BsonDocument>.Filter.Eq("period.id", ObjectId.Parse(periodId));
        var docs = await context.CourseOfferings.Find(filter).ToListAsync(ct);
        return docs.Select(ToDomain).ToList();
    }

    public async Task AddAsync(CourseOffering offering, CancellationToken ct) => await context.CourseOfferings.InsertOneAsync(ToBson(offering), cancellationToken: ct);

    public async Task UpdateAsync(CourseOffering offering, CancellationToken ct) =>
        await context.CourseOfferings.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(offering.Id)), ToBson(offering), cancellationToken: ct);

    public async Task DeleteAsync(string id, CancellationToken ct) =>
        await context.CourseOfferings.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id)), ct);

    internal static BsonDocument ToBson(CourseOffering offering) => new()
    {
        ["_id"] = ObjectId.Parse(offering.Id),
        ["period"] = new BsonDocument { ["id"] = ObjectId.Parse(offering.Period.Id), ["code"] = offering.Period.Code },
        ["subject"] = new BsonDocument { ["id"] = ObjectId.Parse(offering.Subject.Id), ["name"] = offering.Subject.Name, ["termNumber"] = offering.Subject.TermNumber ?? (BsonValue)BsonNull.Value },
        ["region"] = new BsonDocument { ["id"] = ObjectId.Parse(offering.Region.Id), ["code"] = offering.Region.Code, ["name"] = offering.Region.Name },
        ["teacher"] = new BsonDocument { ["id"] = ObjectId.Parse(offering.Teacher.Id), ["enrollmentNumber"] = offering.Teacher.EnrollmentNumber, ["name"] = offering.Teacher.Name },
        ["enrollments"] = new BsonArray(offering.Enrollments.Select(e => new BsonDocument
        {
            ["studentId"] = ObjectId.Parse(e.StudentId),
            ["enrollmentNumber"] = e.EnrollmentNumber,
            ["studentName"] = e.StudentName,
            ["termAtEnrollment"] = e.TermAtEnrollment.Value,
            ["grade"] = e.Grade?.Value ?? (BsonValue)BsonNull.Value,
            ["gradedAt"] = e.GradedAtUtc.HasValue ? e.GradedAtUtc.Value : BsonNull.Value,
            ["gradedBy"] = e.GradedByUserId is null ? BsonNull.Value : ObjectId.Parse(e.GradedByUserId),
            ["enrolledAt"] = e.EnrolledAtUtc,
            ["status"] = e.Status.ToString(),
        })),
        ["enrollmentCount"] = offering.EnrollmentCount,
        ["version"] = 1,
        ["createdAt"] = DateTime.UtcNow,
        ["updatedAt"] = DateTime.UtcNow,
    };

    internal static CourseOffering ToDomain(BsonDocument doc)
    {
        var periodDoc = doc["period"].AsBsonDocument;
        var subjectDoc = doc["subject"].AsBsonDocument;
        var regionDoc = doc["region"].AsBsonDocument;
        var teacherDoc = doc["teacher"].AsBsonDocument;

        var enrollments = doc["enrollments"].AsBsonArray.Select(v =>
        {
            var e = v.AsBsonDocument;
            return Enrollment.Rehydrate(
                e["studentId"].AsObjectId.ToString(),
                e["enrollmentNumber"].AsInt32,
                e["studentName"].AsString,
                TermNumber.Create(e["termAtEnrollment"].AsInt32).Value,
                e.TryGetValue("grade", out var g) && !g.IsBsonNull ? Grade.Create(g.AsInt32).Value : null,
                e.TryGetValue("gradedAt", out var ga) && !ga.IsBsonNull ? ga.ToUniversalTime() : (DateTime?)null,
                e.TryGetValue("gradedBy", out var gb) && !gb.IsBsonNull ? gb.AsObjectId.ToString() : null,
                e["enrolledAt"].ToUniversalTime(),
                Enum.Parse<EnrollmentStatus>(e["status"].AsString));
        });

        return CourseOffering.Rehydrate(
            doc["_id"].AsObjectId.ToString(),
            new PeriodRef(periodDoc["id"].AsObjectId.ToString(), periodDoc["code"].AsString),
            new SubjectRef(subjectDoc["id"].AsObjectId.ToString(), subjectDoc["name"].AsString, subjectDoc.TryGetValue("termNumber", out var tn) && !tn.IsBsonNull ? tn.AsInt32 : null),
            new RegionRefAcademics(regionDoc["id"].AsObjectId.ToString(), regionDoc["code"].AsInt32, regionDoc["name"].AsString),
            new TeacherRef(teacherDoc["id"].AsObjectId.ToString(), teacherDoc["enrollmentNumber"].AsInt32, teacherDoc["name"].AsString),
            enrollments);
    }
}
