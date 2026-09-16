using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.GetImportBatch;
using Shekinah.Application.Billing.GetNotices;
using Shekinah.Application.Billing.GetPaymentMatrix;
using Shekinah.Application.Billing.ImportPayments;
using Shekinah.Application.Billing.IssueNotices;

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
    }

    public sealed record IssueNoticesRequest(string? StudentId);
}
