using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Calendar;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Calendar.CreateCalendarEvent;

/// <summary>
/// Plan de control escolar, fase 7: calendario institucional público. Administrator puede crear
/// eventos generales (sin región, visibles a todos) o de una región específica; RegionalCoordinator
/// y RegionalSecretary solo pueden crear eventos de SU PROPIA región — forzado en el handler vía
/// <see cref="IRegionScopeResolver"/> (RN-09), nunca confiando en el <see cref="RegionId"/> recibido
/// del cliente para esos roles.
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record CreateCalendarEventCommand(
    string Title, string? Description, DateTime StartAtUtc, DateTime? EndAtUtc, string? RegionId)
    : ICommand<CreateCalendarEventResponse>;

public sealed record CreateCalendarEventResponse(string CalendarEventId);

public sealed class CreateCalendarEventCommandValidator : AbstractValidator<CreateCalendarEventCommand>
{
    public CreateCalendarEventCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
    }
}

public sealed class CreateCalendarEventCommandHandler(
    ICalendarEventRepository calendarEvents, IRegionRepository regions,
    IRegionScopeResolver regionScope, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<CreateCalendarEventCommand, CreateCalendarEventResponse>
{
    public async Task<Result<CreateCalendarEventResponse>> HandleAsync(CreateCalendarEventCommand command, CancellationToken ct)
    {
        // RN-09: para Coordinador/Secretario regional, la región queda fijada a la suya propia sin
        // importar lo que haya mandado el cliente. Administrator sí puede elegir cualquier región o
        // ninguna (evento general/institucional).
        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        var effectiveRegionId = mandatoryRegionId ?? command.RegionId;

        RegionRef? region = null;
        if (!string.IsNullOrWhiteSpace(effectiveRegionId))
        {
            var regionEntity = await regions.GetByIdAsync(effectiveRegionId, ct);
            if (regionEntity is null)
            {
                return Result.Failure<CreateCalendarEventResponse>(Error.Validation("CalendarEvent.RegionNotFound", "La región indicada no existe."));
            }

            region = new RegionRef(regionEntity.Id, regionEntity.Name);
        }

        var eventResult = CalendarEvent.Schedule(
            EntityId.NewId(), command.Title, command.Description, command.StartAtUtc, command.EndAtUtc,
            region, currentUser.UserId ?? "system", clock);

        if (eventResult.IsFailure)
        {
            return Result.Failure<CreateCalendarEventResponse>(eventResult.Error);
        }

        await calendarEvents.AddAsync(eventResult.Value, ct);

        return Result.Success(new CreateCalendarEventResponse(eventResult.Value.Id));
    }
}
