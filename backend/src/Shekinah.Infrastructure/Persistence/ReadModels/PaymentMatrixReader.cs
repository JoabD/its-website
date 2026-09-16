using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics.Policies;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Read model de la matriz de pagos (spec técnico §3.5): pipeline $match → $lookup a payments →
/// $facet para datos + conteo. Los MESES los calcula AcademicPeriodMonthsCalculator en C# (RN-17)
/// y se inyectan como parámetro del pipeline — la lógica de negocio no vive en la agregación, solo
/// la proyección. Sustituye los ~150 líneas de CTE recursivo del legado por esto + RN-17 (~15 líneas).
/// </summary>
public sealed class PaymentMatrixReader(MongoContext context, Domain.Academics.IAcademicPeriodRepository periods) : IPaymentMatrixReader
{
    public async Task<PagedResult<StudentPaymentRow>> GetMatrixAsync(PaymentMatrixFilter filter, CancellationToken ct)
    {
        var period = await periods.GetByIdAsync(filter.PeriodId, ct);
        var monthCodes = period?.MonthCodes.Select(m => m.Value).ToList() ?? [];

        var studentFilter = Builders<BsonDocument>.Filter.Eq("role", nameof(UserRole.Student));
        if (!string.IsNullOrWhiteSpace(filter.RegionId))
        {
            studentFilter &= Builders<BsonDocument>.Filter.Eq("region.id", ObjectId.Parse(filter.RegionId));
        }

        var total = await context.Users.CountDocumentsAsync(studentFilter, cancellationToken: ct);

        var students = await context.Users.Find(studentFilter)
            .Skip((filter.Page - 1) * filter.PageSize).Limit(filter.PageSize).ToListAsync(ct);

        var rows = new List<StudentPaymentRow>();
        foreach (var student in students)
        {
            var studentId = student["_id"].AsObjectId;
            var paidDocs = await context.Payments.Find(Builders<BsonDocument>.Filter.Eq("student.id", studentId)).ToListAsync(ct);
            var paidMonths = paidDocs.Select(d => d["monthCode"].AsString).ToHashSet();

            var paidByMonth = monthCodes.ToDictionary(m => m, m => paidMonths.Contains(m));
            var monthsDue = monthCodes.Where(m => !paidMonths.Contains(m)).ToList();

            var regionName = student.TryGetValue("region", out var r) && !r.IsBsonNull ? r.AsBsonDocument["name"].AsString : string.Empty;

            rows.Add(new StudentPaymentRow(
                studentId.ToString(), student["enrollmentNumber"].AsInt32, student["profile"]["fullName"].AsString,
                regionName, paidByMonth, monthsDue));
        }

        return new PagedResult<StudentPaymentRow>(rows, filter.Page, filter.PageSize, total) { Months = monthCodes };
    }
}
