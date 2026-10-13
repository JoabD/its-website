using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.AssignTeacherRole;

/// <summary>
/// "Docentes" → "Ya tiene cuenta" (2026-10, pedido explícito del cliente): da el rol de Profesor a
/// alguien que YA existe en el sistema con otro rol, SIN quitárselo — "si es Admin así que se
/// quede, puede mantener el rol admin y el de maestro". A propósito es un comando distinto de
/// <see cref="UpdateUser.UpdateUserCommand"/> (que SÍ reemplaza el rol principal): este nunca toca
/// Role/Region/Modality/Status, solo agrega Teacher a ExtraRoles y fusiona las regiones de
/// enseñanza (ver <see cref="Domain.Identity.User.AddTeachingRole"/>).
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record AssignTeacherRoleCommand(string UserId, IReadOnlyList<string> RegionIds) : ICommand<Unit>;

public sealed class AssignTeacherRoleCommandValidator : AbstractValidator<AssignTeacherRoleCommand>
{
    public AssignTeacherRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RegionIds).Must(ids => ids is { Count: > 0 }).WithMessage("Selecciona al menos una región.");
    }
}

public sealed class AssignTeacherRoleCommandHandler(
    IUserRepository users, Domain.Catalog.IRegionRepository regions, ICurrentUser currentUser)
    : ICommandHandler<AssignTeacherRoleCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(AssignTeacherRoleCommand command, CancellationToken ct)
    {
        // Mismo principio que UpdateUserCommandHandler: nadie se asigna roles a sí mismo desde este panel.
        if (currentUser.UserId == command.UserId)
        {
            return Result.Failure<Unit>(Error.Forbidden("AssignTeacherRole.CannotEditSelf", "No puedes asignarte este rol a ti mismo desde este panel."));
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        var regionRefs = new List<RegionRef>();
        foreach (var regionId in command.RegionIds.Distinct())
        {
            var regionEntity = await regions.GetByIdAsync(regionId, ct);
            if (regionEntity is null)
            {
                return Result.Failure<Unit>(Error.Validation("AssignTeacherRole.InvalidRegion", "Una de las regiones indicadas no existe."));
            }

            regionRefs.Add(new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name));
        }

        user.AddTeachingRole(regionRefs);
        await users.UpdateAsync(user, ct);

        return Result.Success(Unit.Value);
    }
}
