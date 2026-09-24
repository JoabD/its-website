using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.UpdateUser;

[RequireRole(UserRole.Administrator)]
public sealed record UpdateUserCommand(string UserId, UserRole Role, string? RegionId, Modality? Modality, string? Status) : ICommand<Unit>;

public sealed class UpdateUserCommandHandler(IUserRepository users, Domain.Catalog.IRegionRepository regions, ICurrentUser currentUser)
    : ICommandHandler<UpdateUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(UpdateUserCommand command, CancellationToken ct)
    {
        // Mismo principio que ResetPasswordCommandHandler: nadie edita su propio rol/región/estatus
        // desde el panel de administración de usuarios (evita, por ejemplo, que un Administrator se
        // autodesactive o se quite el rol por error, dejando el sistema sin administradores).
        if (currentUser.UserId == command.UserId)
        {
            return Result.Failure<Unit>(Error.Forbidden("UpdateUser.CannotEditSelf", "No puedes editar tu propio usuario desde este panel."));
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        RegionRef? region = null;
        if (!string.IsNullOrWhiteSpace(command.RegionId))
        {
            var regionEntity = await regions.GetByIdAsync(command.RegionId, ct);
            if (regionEntity is null)
            {
                return Result.Failure<Unit>(Error.Validation("UpdateUser.InvalidRegion", "La región indicada no existe."));
            }

            region = new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name);
        }

        var result = user.UpdateRoleAndScope(command.Role, region, command.Modality);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        if (command.Status == "Inactive") user.Deactivate();
        if (command.Status == "Active") user.Reactivate();

        await users.UpdateAsync(user, ct);
        return Result.Success(Unit.Value);
    }
}
