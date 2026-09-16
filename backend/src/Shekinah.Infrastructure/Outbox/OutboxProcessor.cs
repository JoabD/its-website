using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MongoDB.Bson;
using MongoDB.Driver;
using Polly;
using Polly.Retry;
using Shekinah.Infrastructure.Persistence;

namespace Shekinah.Infrastructure.Outbox;

/// <summary>
/// BackgroundService que drena outboxMessages con reintentos exponenciales (Polly, spec técnico §3.7).
/// Los mensajes "Dead" tras agotar reintentos quedan para revisión manual (no se pierden ni bloquean el resto).
/// </summary>
public sealed class OutboxProcessor(
    MongoContext context, IOptions<EmailSettings> emailOptions, ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly ResiliencePipeline RetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 2, Delay = TimeSpan.FromSeconds(2), BackoffType = DelayBackoffType.Exponential })
        .Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error inesperado procesando el outbox.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("status", "Pending") &
                     Builders<BsonDocument>.Filter.Lte("nextAttemptAt", DateTime.UtcNow);

        var pending = await context.OutboxMessages.Find(filter).Limit(50).ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                await RetryPipeline.ExecuteAsync(async token => await SendEmailAsync(message, token), ct);

                var update = Builders<BsonDocument>.Update.Set("status", "Processed").Set("processedAt", DateTime.UtcNow);
                await context.OutboxMessages.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", message["_id"]), update, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                var attempts = message.GetValue("attempts", 0).AsInt32 + 1;
                var status = attempts >= MaxAttempts ? "Dead" : "Pending";
                var nextAttempt = DateTime.UtcNow.AddSeconds(Math.Pow(2, attempts) * 5);

                var update = Builders<BsonDocument>.Update
                    .Set("attempts", attempts).Set("status", status).Set("nextAttemptAt", nextAttempt).Set("lastError", ex.Message);
                await context.OutboxMessages.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", message["_id"]), update, cancellationToken: ct);

                logger.LogWarning(ex, "Fallo enviando mensaje de outbox {MessageId}, intento {Attempts}", message["_id"], attempts);
            }
        }
    }

    private async Task SendEmailAsync(BsonDocument message, CancellationToken ct)
    {
        var payload = message["payload"].AsBsonDocument;
        var opts = emailOptions.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(opts.SenderDisplayName, opts.SenderEmail));
        mime.To.Add(MailboxAddress.Parse(payload["to"].AsString));
        if (payload.TryGetValue("cc", out var ccValue) && !ccValue.IsBsonNull)
        {
            mime.Cc.Add(MailboxAddress.Parse(ccValue.AsString));
        }

        mime.Subject = payload["subject"].AsString;
        mime.Body = new TextPart("html") { Text = payload["htmlBody"].AsString };

        // Puerto 587 ⇒ STARTTLS (Gmail no acepta SSL implícito de 465 con contraseña de aplicación).
        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(opts.SmtpHost, opts.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(opts.SenderEmail, opts.SenderAppPassword, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
