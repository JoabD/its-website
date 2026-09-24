using Microsoft.Extensions.Configuration;
using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.GetBillingSummary;
using Shekinah.Application.Billing.GetImportBatch;
using Shekinah.Application.Billing.GetNotices;
using Shekinah.Application.Billing.GetPaymentMatrix;
using Shekinah.Application.Billing.ImportPayments;
using Shekinah.Application.Billing.IssueNotices;
using Shekinah.Application.Billing.RegisterManualPayment;
using Shekinah.Application.Billing.RunScheduledNotices;
using Shekinah.Application.Billing.SendPaymentReceipt;
using Shekinah.Application.Billing.UndoManualPayment;

namespace Shekinah.Api.Endpoints;

public static class PaymentsEndpoints
{
    public static void MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payments").WithTags("Pagos").RequireAuthorization();

        group.MapGet("/matrix", async (string periodId, string? regionId, int page, int pageSize, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetPaymentMatrixQuery(periodId, regionId, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToApiResult());

        group.MapPost("/import", async (IFormFile file, IDispatcher dispatcher, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var command = new ImportPaymentsCommand(file.FileName, stream);
            return (await dispatcher.SendAsync(command, ct)).ToApiResult();
        }).DisableAntiforgery();

        group.MapGet("/import/{batchId}", async (string batchId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetImportBatchQuery(batchId), ct)).ToApiResult());

        group.MapPost("/notices", async (IssueNoticesRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new IssueNoticesCommand(request.StudentId), ct)).ToApiResult());

        group.MapGet("/notices", async (string? studentId, int page, int pageSize, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetNoticesQuery(studentId, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToApiResult());

        // Fase 3 del plan de control escolar: mientras no hay pasarela en línea, marcar/revertir un
        // mes como pagado directamente desde la matriz (sin pasar por importación masiva).
        group.MapPost("/manual", async (ManualPaymentRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RegisterManualPaymentCommand(request.StudentId, request.MonthCode), ct)).ToApiResult());

        group.MapDelete("/manual", async (string studentId, string monthCode, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new UndoManualPaymentCommand(studentId, monthCode), ct)).ToApiResult());

        // Panel de verificación de pagos (docs/Plan-Panel-Pagos.md, fase 1): recibo en PDF, enviado
        // por correo (automático) + datos para abrir WhatsApp (wa.me, un clic humano — Opción C).
        group.MapPost("/receipt", async (SendPaymentReceiptRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SendPaymentReceiptCommand(request.StudentId, request.MonthCode), ct)).ToApiResult());

        // Panel de verificación de pagos, sección 8: resumen financiero (cobrado vs. esperado, por región).
        group.MapGet("/summary", async (string monthCode, string? regionId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetBillingSummaryQuery(monthCode, regionId), ct)).ToApiResult());

        // Panel de verificación de pagos, sección 7: disparado UNA VEZ POR SEMANA por un GitHub
        // Actions `schedule` (ver .github/workflows/weekly-payment-notices.yml) — nunca por un
        // usuario logueado, así que no lleva RequireAuthorization: se protege con un secreto
        // compartido en un header (Jobs:ScheduledNoticesSecret, configurado en Azure App Service,
        // nunca en este repo) en vez de un rol, porque no hay sesión detrás de un cron.
        group.MapPost("/notices/scheduled", async (HttpRequest request, IConfiguration configuration, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var expectedSecret = configuration["Jobs:ScheduledNoticesSecret"];
            if (string.IsNullOrEmpty(expectedSecret) || request.Headers["X-Job-Secret"] != expectedSecret)
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(new RunScheduledNoticesCommand(), ct)).ToApiResult();
        }).AllowAnonymous();
    }

    public sealed record IssueNoticesRequest(string? StudentId);

    public sealed record ManualPaymentRequest(string StudentId, string MonthCode);

    public sealed record SendPaymentReceiptRequest(string StudentId, string MonthCode);
}
