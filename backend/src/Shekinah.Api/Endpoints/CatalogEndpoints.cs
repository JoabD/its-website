using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Catalog.GetCurriculum;
using Shekinah.Application.Catalog.GetRegions;
using Shekinah.Application.Catalog.ManageSubjects;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/catalog").WithTags("Catálogo");

        group.MapGet("/regions", async (Modality? modality, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetRegionsQuery(modality), ct)).ToApiResult()).AllowAnonymous();

        group.MapGet("/curriculum", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetCurriculumQuery(), ct)).ToApiResult()).AllowAnonymous();

        group.MapGet("/subjects", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetCurriculumQuery(), ct)).ToApiResult()).RequireAuthorization();

        group.MapPost("/subjects", async (CreateSubjectCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created)).RequireAuthorization();

        group.MapPut("/subjects/{id}/syllabus", async (string id, PublishSyllabusRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new PublishSyllabusCommand(id, request.PdfUrl), ct)).ToApiResult()).RequireAuthorization();

        group.MapDelete("/subjects/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new DeleteSubjectCommand(id), ct)).ToApiResult()).RequireAuthorization();
    }

    public sealed record PublishSyllabusRequest(string PdfUrl);
}
