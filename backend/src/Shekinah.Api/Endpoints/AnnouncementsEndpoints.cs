using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Announcements.CreateAnnouncement;
using Shekinah.Application.Announcements.GetAnnouncements;

namespace Shekinah.Api.Endpoints;

/// <summary>Plan de control escolar, fase 6: avisos institucionales (distinto de los avisos de
/// morosidad de /payments/notices).</summary>
public static class AnnouncementsEndpoints
{
    public static void MapAnnouncementsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/announcements").WithTags("Avisos").RequireAuthorization();

        group.MapGet("/", async (int limit, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetAnnouncementsQuery(limit), ct)).ToApiResult());

        group.MapPost("/", async (CreateAnnouncementCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created));
    }
}
