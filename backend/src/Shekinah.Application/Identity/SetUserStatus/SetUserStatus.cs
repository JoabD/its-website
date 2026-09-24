using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.SetUserStatus;

/// <summary>
/// Panel de Usuarios → inhabilitar/reactivar (side panel). Deliberadamente separado de
/// <see cref="UpdateUser.UpdateUserCommand"/> (que también cambia Status, pero junto con rol/región):
/// la lista de usuarios de este panel no trae RegionId (solo RegionName, para mostrar), así que
/// reusar ese comando obligaría a resolver la región de nuevo solo para no tocarla. Un comando que
/// SOLO cambia el estatus es más simple y no arriesga alterar rol/región por accidente.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record SetUserStatusCommand(string UserId, bool Active) : ICommand<Unit>;

public sealed class SetUserStatusCommandHandler(IUserRepository users, ICurrentUser currentUser) : ICommandHandler<SetUserStatusCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(SetUserStatusCommand command, CancellationToken ct)
    {
        if (currentUser.UserId == command.UserId)
        {
            return Result.Failure<Unit>(Error.Forbidden("SetUserStatus.CannotEditSelf", "No puedes inhabilitar tu propio usuario."));
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        if (user.Role == UserRole.Student)
        {
            return Result.Failure<Unit>(Error.Validation("SetUserStatus.NotAStaffUser", "Los alumnos no se administran desde este panel."));
        }

        if (command.Active) user.Reactivate(); else user.Deactivate();

        await users.UpdateAsync(user, ct);
        return Result.Success(Unit.Value);
    }
}
