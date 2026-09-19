using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Admissions.ApproveApplication;
using Shekinah.Application.Admissions.DeleteApplication;
using Shekinah.Application.Admissions.GetApplicationById;
using Shekinah.Application.Admissions.GetApplications;
using Shekinah.Application.Admissions.ManageChecklistItems;
using Shekinah.Application.Admissions.RejectApplication;
using Shekinah.Application.Admissions.SubmitApplication;
using Shekinah.Application.Admissions.UpdateChecklist;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Api.Endpoints;

public static class AdmissionsEndpoints
{
    public static void MapAdmissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admissions").WithTags("Admisiones");

        group.MapPost("/applications", async (SubmitApplicationCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created)).AllowAnonymous();

        // Mismo BUG REAL que en UsersEndpoints (ver ahí el detalle): "page"/"pageSize" sin valor por
        // defecto obligan a Minimal API a exigirlos siempre en el query string, o revienta con
        // BadHttpRequestException antes de ejecutar el handler. Hoy el frontend siempre los manda,
        // pero se corrige preventivamente para no repetir el mismo susto.
        group.MapGet("/applications", async (ApplicationStatus? status, string? search, IDispatcher dispatcher, CancellationToken ct, int page = 0, int pageSize = 0) =>
            (await dispatcher.QueryAsync(new GetApplicationsQuery(status, search, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToApiResult())
            .RequireAuthorization();

        group.MapGet("/applications/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetApplicationByIdQuery(id), ct)).ToApiResult()).RequireAuthorization();

        // viaQuickAction=true: aprobar directo desde la lista, sin exigir el checklist (RN de
        // producto explícita — se infiere que ya se entregó todo). El panel de revisión llama este
        // mismo endpoint con viaQuickAction=false una vez que el checklist está completo.
        group.MapPost("/applications/{id}/approve", async (string id, ApproveRequest? request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ApproveApplicationCommand(id, request?.ViaQuickAction ?? false), ct)).ToApiResult()).RequireAuthorization();

        group.MapPost("/applications/{id}/reject", async (string id, RejectRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RejectApplicationCommand(id, request.Reason), ct)).ToApiResult()).RequireAuthorization();

        group.MapPatch("/applications/{id}/checklist", async (string id, ChecklistRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new UpdateChecklistCommand(id, request.CheckedItemIds), ct)).ToApiResult())
            .RequireAuthorization();

        group.MapDelete("/applications/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new DeleteApplicationCommand(id), ct)).ToApiResult()).RequireAuthorization();

        // Configuración → Documentos de inscripción (catálogo editable del checklist, §3 de la spec).
        var checklistItemsGroup = app.MapGroup("/api/v1/admissions/checklist-items").WithTags("Admisiones");

        checklistItemsGroup.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetChecklistItemsQuery(), ct)).ToApiResult()).RequireAuthorization();

        checklistItemsGroup.MapPost("/", async (CreateChecklistItemRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new CreateChecklistItemCommand(request.Label), ct)).ToApiResult(StatusCodes.Status201Created))
            .RequireAuthorization();

        checklistItemsGroup.MapPut("/{id}", async (string id, UpdateChecklistItemRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new UpdateChecklistItemCommand(id, request.Label, request.DisplayOrder), ct)).ToApiResult())
            .RequireAuthorization();

        checklistItemsGroup.MapPatch("/{id}/active", async (string id, SetChecklistItemActiveRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SetChecklistItemActiveCommand(id, request.IsActive), ct)).ToApiResult())
            .RequireAuthorization();
    }

    public sealed record RejectRequest(string Reason);

    public sealed record ApproveRequest(bool ViaQuickAction);

    public sealed record ChecklistRequest(IReadOnlyList<string> CheckedItemIds);

    public sealed record CreateChecklistItemRequest(string Label);

    public sealed record UpdateChecklistItemRequest(string Label, int DisplayOrder);

    public sealed record SetChecklistItemActiveRequest(bool IsActive);
}
