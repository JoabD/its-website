using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Calendar;

namespace Shekinah.Infrastructure.Persistence.Repositories;

public sealed class CalendarEventRepository(MongoContext context) : ICalendarEventRepository
{
    public async Task AddAsync(CalendarEvent calendarEvent, CancellationToken ct)
    {
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.Parse(calendarEvent.Id),
            ["title"] = calendarEvent.Title,
            ["description"] = calendarEvent.Description is null ? BsonNull.Value : calendarEvent.Description,
            ["startAt"] = calendarEvent.StartAtUtc,
            ["endAt"] = calendarEvent.EndAtUtc is null ? BsonNull.Value : calendarEvent.EndAtUtc.Value,
            ["regionId"] = calendarEvent.Region is null ? BsonNull.Value : calendarEvent.Region.Id,
            ["regionName"] = calendarEvent.Region is null ? BsonNull.Value : calendarEvent.Region.Name,
            ["createdBy"] = calendarEvent.CreatedByUserId,
            ["createdAt"] = calendarEvent.CreatedAtUtc,
        };
        await context.CalendarEvents.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetUpcomingAsync(DateTime fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Gte("startAt", fromUtc);
        if (toUtc is not null)
        {
            filter &= Builders<BsonDocument>.Filter.Lte("startAt", toUtc.Value);
        }

        var docs = await context.CalendarEvents.Find(filter).SortBy(d => d["startAt"]).ToListAsync(ct);

        return docs.Select(ToDomain).ToList();
    }

    public async Task<CalendarEvent?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;

        var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
        var doc = await context.CalendarEvents.Find(filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return;

        var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
        await context.CalendarEvents.DeleteOneAsync(filter, ct);
    }

    private static CalendarEvent ToDomain(BsonDocument doc)
    {
        RegionRef? region = doc["regionId"].IsBsonNull
            ? null
            : new RegionRef(doc["regionId"].AsString, doc["regionName"].AsString);

        return CalendarEvent.Rehydrate(
            doc["_id"].AsObjectId.ToString(),
            doc["title"].AsString,
            doc["description"].IsBsonNull ? null : doc["description"].AsString,
            doc["startAt"].ToUniversalTime(),
            doc["endAt"].IsBsonNull ? null : doc["endAt"].ToUniversalTime(),
            region,
            doc["createdBy"].AsString,
            doc["createdAt"].ToUniversalTime());
    }
}
