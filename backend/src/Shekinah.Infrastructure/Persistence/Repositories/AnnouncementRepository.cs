using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Domain.Announcements;

namespace Shekinah.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementRepository(MongoContext context) : IAnnouncementRepository
{
    public async Task AddAsync(Announcement announcement, CancellationToken ct)
    {
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.Parse(announcement.Id),
            ["title"] = announcement.Title,
            ["body"] = announcement.Body,
            ["publishedBy"] = announcement.PublishedByUserId,
            ["publishedAt"] = announcement.PublishedAtUtc,
            ["emailedToTeachers"] = announcement.EmailedToTeachers,
        };
        await context.Announcements.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Announcement>> GetRecentAsync(int limit, CancellationToken ct)
    {
        var docs = await context.Announcements.Find(Builders<BsonDocument>.Filter.Empty)
            .SortByDescending(d => d["publishedAt"]).Limit(limit).ToListAsync(ct);

        return docs.Select(doc => Announcement.Rehydrate(
            doc["_id"].AsObjectId.ToString(), doc["title"].AsString, doc["body"].AsString,
            doc["publishedBy"].AsString, doc["publishedAt"].ToUniversalTime(), doc["emailedToTeachers"].AsBoolean)).ToList();
    }
}
