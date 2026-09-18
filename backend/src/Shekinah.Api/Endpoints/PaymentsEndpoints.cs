using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.GetImportBatch;
using Shekinah.Application.Billing.GetNotices;
using Shekinah.Application.Billing.GetPaymentMatrix;
using Shekinah.Application.Billing.ImportPayments;
using Shekinah.Application.Billing.IssueNotices;
using Shekinah.Application.Billing.RegisterManualPayment;
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
    }

    public sealed record IssueNoticesRequest(string? StudentId);

    public sealed record ManualPaymentRequest(string StudentId, string MonthCode);
}
