using MongoDB.Bson;
using Shekinah.Application.Abstractions;
using Shekinah.Infrastructure.Persistence;

namespace Shekinah.Infrastructure.Outbox;

/// <summary>
/// RN-04 / spec técnico §3.7: los correos NUNCA se envían dentro de la transacción de negocio. Se
/// escribe en outboxMessages y un BackgroundService (<see cref="OutboxProcessor"/>) los procesa con
/// reintentos exponenciales (Polly). Un SMTP lento o caído ya no cuelga ni corrompe la petición.
/// </summary>
public sealed class OutboxEmailSender(MongoContext context) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct, string? cc = null, IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var payload = new BsonDocument { ["to"] = to, ["subject"] = subject, ["htmlBody"] = htmlBody };
        if (!string.IsNullOrWhiteSpace(cc))
        {
            payload["cc"] = cc;
        }

        if (attachments is { Count: > 0 })
        {
            payload["attachments"] = new BsonArray(attachments.Select(a => new BsonDocument
            {
                ["fileName"] = a.FileName,
                ["contentType"] = a.ContentType,
                ["content"] = new BsonBinaryData(a.Content),
            }));
        }

        var doc = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["type"] = "Email",
            ["payload"] = payload,
            ["occurredAt"] = DateTime.UtcNow,
            ["processedAt"] = BsonNull.Value,
            ["attempts"] = 0,
            ["nextAttemptAt"] = DateTime.UtcNow,
            ["lastError"] = BsonNull.Value,
            ["status"] = "Pending",
        };

        await context.OutboxMessages.InsertOneAsync(doc, cancellationToken: ct);
    }
}
