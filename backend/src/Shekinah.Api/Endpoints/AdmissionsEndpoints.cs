using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Admissions.ApproveApplication;
using Shekinah.Application.Admissions.GetApplicationById;
using Shekinah.Application.Admissions.GetApplications;
using Shekinah.Application.Admissions.RejectApplication;
using Shekinah.Application.Admissions.SubmitApplication;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Api.Endpoints;

public static class AdmissionsEndpoints
{
    public static void MapAdmissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admissions").WithTags("Admisiones");

        group.MapPost("/applications", async (SubmitApplicationCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created)).AllowAnonymous();

        group.MapGet("/applications", async (ApplicationStatus? status, string? search, int page, int pageSize, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetApplicationsQuery(status, search, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToApiResult())
            .RequireAuthorization();

        group.MapGet("/applications/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetApplicationByIdQuery(id), ct)).ToApiResult()).RequireAuthorization();

        group.MapPost("/applications/{id}/approve", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ApproveApplicationCommand(id), ct)).ToApiResult()).RequireAuthorization();

        group.MapPost("/applications/{id}/reject", async (string id, RejectRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RejectApplicationCommand(id, request.Reason), ct)).ToApiResult()).RequireAuthorization();
    }

    public sealed record RejectRequest(string Reason);
}
