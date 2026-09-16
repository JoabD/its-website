using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Application.Identity.RefreshToken;
using Shekinah.Infrastructure.Persistence;

namespace Shekinah.Infrastructure.Auth;

/// <summary>Colección refreshTokens con TTL sobre expiresAt (spec técnico §5.3): Mongo expira el documento solo.</summary>
public sealed class MongoRefreshTokenStore(MongoContext context) : IRefreshTokenStore
{
    public async Task<string?> GetUserIdForValidTokenAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var filter = Builders<BsonDocument>.Filter.Eq("tokenHash", hash) & Builders<BsonDocument>.Filter.Eq("revokedAt", BsonNull.Value);
        var doc = await context.RefreshTokens.Find(filter).FirstOrDefaultAsync(ct);

        if (doc is null || doc["expiresAt"].ToUniversalTime() < DateTime.UtcNow)
        {
            return null;
        }

        return doc["userId"].AsObjectId.ToString();
    }

    public async Task RevokeAsync(string refreshToken, string? replacedByHash, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var update = Builders<BsonDocument>.Update.Set("revokedAt", DateTime.UtcNow)
            .Set("replacedByTokenHash", replacedByHash is null ? (BsonValue)BsonNull.Value : Hash(replacedByHash));
        await context.RefreshTokens.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("tokenHash", hash), update, cancellationToken: ct);
    }

    public async Task StoreAsync(string userId, string refreshTokenHash, DateTime expiresAtUtc, CancellationToken ct)
    {
        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["userId"] = ObjectId.Parse(userId),
            ["tokenHash"] = Hash(refreshTokenHash),
            ["expiresAt"] = expiresAtUtc,
            ["createdAt"] = DateTime.UtcNow,
            ["revokedAt"] = BsonNull.Value,
            ["replacedByTokenHash"] = BsonNull.Value,
        };
        await context.RefreshTokens.InsertOneAsync(doc, cancellationToken: ct);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
