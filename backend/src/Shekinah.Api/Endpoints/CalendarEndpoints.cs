using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Calendar.CreateCalendarEvent;
using Shekinah.Application.Calendar.DeleteCalendarEvent;
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
        // desde otro punto en el futuro. `to` (opcional) permite al panel admin visual pedir solo el
        // rango del mes que se está mostrando en vez de "todo lo próximo" (mejora del calendario
        // visual con angular-calendar: navegar meses hacia atrás también debe traer datos).
        group.MapGet("/", async (DateTime? from, DateTime? to, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetPublicCalendarEventsQuery(from, to), ct)).ToApiResult());

        group.MapPost("/", async (CreateCalendarEventCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created))
            .RequireAuthorization();

        group.MapDelete("/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new DeleteCalendarEventCommand(id), ct)).ToApiResult())
            .RequireAuthorization();
    }
}
