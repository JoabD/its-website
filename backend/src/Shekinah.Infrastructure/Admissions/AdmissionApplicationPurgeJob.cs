using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Shekinah.Infrastructure.Persistence;

namespace Shekinah.Infrastructure.Admissions;

/// <summary>
/// Purga automática de solicitudes de admisión ya decididas (spec confirmada: 30 días fijos tras
/// aprobar o rechazar, sin excepción manual — no hay botón para posponerla). Corre una vez al día
/// dentro del mismo proceso de la API, igual que <c>OutboxProcessor</c> — sin infraestructura nueva.
///
/// Solo elimina el documento <c>AdmissionApplication</c> (la ficha cruda del wizard público). El
/// <c>User</c>/alumno que ya se haya creado al aprobar NUNCA se toca aquí — vive para siempre con su
/// matrícula, kardex y pagos, sin relación con esta purga.
/// </summary>
public sealed class AdmissionApplicationPurgeJob(MongoContext context, ILogger<AdmissionApplicationPurgeJob> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeDueApplicationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error inesperado purgando solicitudes de admisión vencidas.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task PurgeDueApplicationsAsync(CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Lte("purgeScheduledAt", DateTime.UtcNow) &
                     Builders<BsonDocument>.Filter.Exists("purgeScheduledAt", true) &
                     Builders<BsonDocument>.Filter.Ne("purgeScheduledAt", BsonNull.Value);

        var due = await context.AdmissionApplications.Find(filter).Limit(200).ToListAsync(ct);
        if (due.Count == 0) return;

        var ids = due.Select(d => d["_id"]).ToList();
        var deleteFilter = Builders<BsonDocument>.Filter.In("_id", ids);
        var result = await context.AdmissionApplications.DeleteManyAsync(deleteFilter, ct);

        logger.LogInformation("Purga automática de admisiones: {Count} solicitudes eliminadas (30 días tras decisión).", result.DeletedCount);
    }
}
