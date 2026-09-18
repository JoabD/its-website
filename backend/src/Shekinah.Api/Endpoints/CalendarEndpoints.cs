using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Calendar.CreateCalendarEvent;
using Shekinah.Application.Calendar.GetPublicCalendarEvents;

namespace Shekinah.Api.Endpoints;

/// <summary>Plan de control escolar, fase 7: calendario institucional público (sin login) +
/// administración de eventos.</summary>
public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/calendar").WithTags("Calendario");

        // Público, sin autenticación (RequireAuthorization NO se aplica a este grupo): confirmado
        // por el usuario, "los eventos en el calendario deben poderse ver aún sin necesidad de estar
        // logeados". El caso de uso también está marcado [AllowAnonymousUseCase] por si se invoca
        // desde otro punto en el futuro.
        group.MapGet("/", async (DateTime? from, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetPublicCalendarEventsQuery(from), ct)).ToApiResult());

        group.MapPost("/", async (CreateCalendarEventCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created))
            .RequireAuthorization();
    }
}
