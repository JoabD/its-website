using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Billing;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(MongoContext context) : IPaymentRepository
{
    public async Task<Payment?> FindAsync(string studentId, MonthCode monthCode, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("student.id", ObjectId.Parse(studentId)) &
                     Builders<BsonDocument>.Filter.Eq("monthCode", monthCode.Value);
        var doc = await context.Payments.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<MonthCode>> GetPaidMonthsAsync(string studentId, CancellationToken ct)
    {
        var docs = await context.Payments.Find(Builders<BsonDocument>.Filter.Eq("student.id", ObjectId.Parse(studentId))).ToListAsync(ct);
        return docs.Select(d => MonthCode.Create(d["monthCode"].AsString).Value).ToList();
    }

    public async Task AddAsync(Payment payment, CancellationToken ct) => await context.Payments.InsertOneAsync(ToBson(payment), cancellationToken: ct);

    public async Task DeleteAsync(string paymentId, CancellationToken ct) =>
        await context.Payments.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(paymentId)), ct);

    public async Task<PaymentImportBatch> RegisterImportBatchAsync(string fileName, string uploadedByUserId, int totalRows, CancellationToken ct)
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
        await context.PaymentImportBatches.InsertOneAsync(doc, cancellationToken: ct);
        return new PaymentImportBatch(id.ToString(), fileName, uploadedByUserId, doc["uploadedAt"].ToUniversalTime(), totalRows, 0, ImportBatchStatus.Processing, []);
    }

    public async Task CompleteImportBatchAsync(string batchId, int importedRows, IReadOnlyList<PaymentImportRowError> errors, CancellationToken ct)
    {
        var update = Builders<BsonDocument>.Update
            .Set("importedRows", importedRows)
            .Set("status", nameof(ImportBatchStatus.Completed))
            .Set("errors", new BsonArray(errors.Select(e => new BsonDocument
            {
                ["rowNumber"] = e.RowNumber, ["code"] = e.Code, ["message"] = e.Message, ["rawValues"] = new BsonArray(e.RawValues),
            })));

        await context.PaymentImportBatches.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(batchId)), update, cancellationToken: ct);
    }

    public async Task<PaymentImportBatch?> GetImportBatchAsync(string batchId, CancellationToken ct)
    {
        var doc = await context.PaymentImportBatches.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(batchId))).FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        var errors = doc["errors"].AsBsonArray.Select(e => new PaymentImportRowError(
            e["rowNumber"].AsInt32, e["code"].AsString, e["message"].AsString,
            e["rawValues"].AsBsonArray.Select(v => v.AsString).ToList())).ToList();

        return new PaymentImportBatch(
            doc["_id"].AsObjectId.ToString(), doc["fileName"].AsString, doc["uploadedBy"].AsString, doc["uploadedAt"].ToUniversalTime(),
            doc["totalRows"].AsInt32, doc["importedRows"].AsInt32, Enum.Parse<ImportBatchStatus>(doc["status"].AsString), errors);
    }

    internal static BsonDocument ToBson(Payment payment) => new()
    {
        ["_id"] = ObjectId.Parse(payment.Id),
        ["student"] = new BsonDocument { ["id"] = ObjectId.Parse(payment.Student.Id), ["enrollmentNumber"] = payment.Student.EnrollmentNumber, ["name"] = payment.Student.Name },
        ["monthCode"] = payment.MonthCode.Value,
        ["periodId"] = string.IsNullOrEmpty(payment.PeriodId) ? BsonNull.Value : ObjectId.Parse(payment.PeriodId),
        ["amount"] = payment.Amount.ToBson() ?? (BsonValue)BsonNull.Value,
        ["currency"] = payment.Amount?.Currency ?? "MXN",
        ["source"] = payment.Source.ToString(),
        ["importBatchId"] = payment.ImportBatchId is null ? BsonNull.Value : ObjectId.Parse(payment.ImportBatchId),
        ["registeredAt"] = payment.RegisteredAtUtc,
        ["registeredBy"] = payment.RegisteredByUserId,
        ["version"] = 1,
    };

    internal static Payment ToDomain(BsonDocument doc)
    {
        var studentDoc = doc["student"].AsBsonDocument;
        return Payment.Rehydrate(
            doc["_id"].AsObjectId.ToString(),
            new StudentRef(studentDoc["id"].AsObjectId.ToString(), studentDoc["enrollmentNumber"].AsInt32, studentDoc["name"].AsString),
            MonthCode.Create(doc["monthCode"].AsString).Value,
            doc.TryGetValue("periodId", out var p) && !p.IsBsonNull ? p.AsObjectId.ToString() : string.Empty,
            BsonMappingExtensions.MoneyFromBson(doc.GetValue("amount", BsonNull.Value)),
            Enum.Parse<PaymentSource>(doc["source"].AsString),
            doc.TryGetValue("importBatchId", out var ib) && !ib.IsBsonNull ? ib.AsObjectId.ToString() : null,
            doc["registeredAt"].ToUniversalTime(),
            doc["registeredBy"].AsString);
    }
}

public sealed class PaymentNoticeRepository(MongoContext context) : IPaymentNoticeRepository
{
    public async Task AddAsync(PaymentNotice notice, CancellationToken ct)
    {
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.Parse(notice.Id),
            ["student"] = new BsonDocument { ["id"] = ObjectId.Parse(notice.Student.Id), ["enrollmentNumber"] = notice.Student.EnrollmentNumber, ["name"] = notice.Student.Name },
            ["noticeNumber"] = notice.NoticeNumber,
            ["monthsDue"] = new BsonArray(notice.MonthsDue.Select(m => m.Value)),
            ["periodId"] = ObjectId.Parse(notice.PeriodId),
            ["channel"] = notice.Channel.ToString(),
            ["issuedAt"] = notice.IssuedAtUtc,
            ["issuedBy"] = notice.IssuedByUserId,
            ["resultedInBlock"] = notice.ResultedInBlock,
        };
        await context.PaymentNotices.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task<int> GetNextNoticeNumberAsync(string studentId, CancellationToken ct)
    {
        var count = await context.PaymentNotices.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("student.id", ObjectId.Parse(studentId)), cancellationToken: ct);
        return (int)count + 1;
    }

    public async Task<(IReadOnlyList<PaymentNotice> Items, long TotalCount)> GetHistoryAsync(string? studentId, int page, int pageSize, CancellationToken ct)
    {
        var filter = string.IsNullOrWhiteSpace(studentId)
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Eq("student.id", ObjectId.Parse(studentId));

        var total = await context.PaymentNotices.CountDocumentsAsync(filter, cancellationToken: ct);
        var docs = await context.PaymentNotices.Find(filter).SortByDescending(d => d["issuedAt"])
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);

        var items = docs.Select(doc =>
        {
            var studentDoc = doc["student"].AsBsonDocument;
            return PaymentNotice.Issue(
                doc["_id"].AsObjectId.ToString(),
                new StudentRef(studentDoc["id"].AsObjectId.ToString(), studentDoc["enrollmentNumber"].AsInt32, studentDoc["name"].AsString),
                doc["noticeNumber"].AsInt32,
                doc["monthsDue"].AsBsonArray.Select(v => MonthCode.Create(v.AsString).Value).ToList(),
                doc["periodId"].AsObjectId.ToString(),
                doc["issuedBy"].AsString,
                new FixedClock(doc["issuedAt"].ToUniversalTime())).Value;
        }).ToList();

        return (items, total);
    }

    private sealed class FixedClock(DateTime value) : Domain.Common.IClock
    {
        public DateTime UtcNow => value;
    }
}
