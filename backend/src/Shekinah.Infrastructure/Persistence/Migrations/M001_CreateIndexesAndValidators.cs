using MongoDB.Bson;
using MongoDB.Driver;

namespace Shekinah.Infrastructure.Persistence.Migrations;

/// <summary>
/// Índices exactos de ESPECIFICACION-TECNICA.md §5.3, incluyendo el índice único parcial que
/// impone RN-13 (a lo sumo un AcademicPeriod Active) directamente en la base de datos.
/// </summary>
public sealed class M001_CreateIndexesAndValidators : IMongoMigration
{
    public int Version => 1;

    public string Description => "Crea índices únicos, compuestos, de texto y TTL en todas las colecciones.";

    public async Task UpAsync(IMongoDatabase db, CancellationToken ct)
    {
        var users = db.GetCollection<BsonDocument>("users");
        await users.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("enrollmentNumber"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("profile.email"),
                new CreateIndexOptions<BsonDocument> { Unique = true, PartialFilterExpression = Builders<BsonDocument>.Filter.Exists("profile.email") }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("role").Ascending("status").Ascending("region.id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("role").Ascending("academic.currentTerm").Ascending("region.id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Text("profile.fullName")),
        ], cancellationToken: ct);

        var applications = db.GetCollection<BsonDocument>("admissionApplications");
        await applications.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status").Descending("submittedAt")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("folio"), new CreateIndexOptions { Unique = true }),
        ], cancellationToken: ct);

        var periods = db.GetCollection<BsonDocument>("academicPeriods");
        await periods.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("code"), new CreateIndexOptions { Unique = true }),
            // RN-13 impuesto en la base: a lo sumo un documento con status="Active".
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status"),
                new CreateIndexOptions<BsonDocument> { Unique = true, PartialFilterExpression = Builders<BsonDocument>.Filter.Eq("status", "Active") }),
        ], cancellationToken: ct);

        var subjects = db.GetCollection<BsonDocument>("subjects");
        await subjects.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("code"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("programType").Ascending("termNumber").Ascending("displayOrder")),
        ], cancellationToken: ct);

        var offerings = db.GetCollection<BsonDocument>("courseOfferings");
        await offerings.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("period.id").Ascending("subject.id").Ascending("region.id"),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("teacher.id").Ascending("period.id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("enrollments.studentId").Ascending("period.id")),
        ], cancellationToken: ct);

        var payments = db.GetCollection<BsonDocument>("payments");
        await payments.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("student.id").Ascending("monthCode"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("monthCode")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("importBatchId")),
        ], cancellationToken: ct);

        var notices = db.GetCollection<BsonDocument>("paymentNotices");
        await notices.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("student.id").Descending("issuedAt")), cancellationToken: ct);

        var refreshTokens = db.GetCollection<BsonDocument>("refreshTokens");
        await refreshTokens.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tokenHash"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("expiresAt"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
        ], cancellationToken: ct);

        var outbox = db.GetCollection<BsonDocument>("outboxMessages");
        await outbox.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status").Ascending("nextAttemptAt")), cancellationToken: ct);

        var auditLogs = db.GetCollection<BsonDocument>("auditLogs");
        await auditLogs.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Descending("occurredAt"),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(365) }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("actor.id")),
        ], cancellationToken: ct);
    }
}
