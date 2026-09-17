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

    public async Task<IReadOnlyList<CalendarEvent>> GetUpcomingAsync(DateTime fromUtc, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Gte("startAt", fromUtc);
        var docs = await context.CalendarEvents.Find(filter).SortBy(d => d["startAt"]).ToListAsync(ct);

        return docs.Select(ToDomain).ToList();
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
