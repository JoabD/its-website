using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.DeleteUser;

/// <summary>
/// Panel de Usuarios → "Eliminar usuario" (pedido explícito del cliente, además de inhabilitar).
/// RN-12: solo Administrator; nunca sobre uno mismo; nunca sobre un alumno (mismo alcance que
/// AdminUpdateUserCommand — Alumnos no se administra desde aquí); y nunca el último Administrator
/// activo, para no dejar el sistema sin nadie que pueda volver a entrar a este panel.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record DeleteUserCommand(string UserId) : ICommand<Unit>;

public sealed class DeleteUserCommandHandler(IUserRepository users, ICurrentUser currentUser) : ICommandHandler<DeleteUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteUserCommand command, CancellationToken ct)
    {
        if (currentUser.UserId == command.UserId)
        {
            return Result.Failure<Unit>(Error.Forbidden("DeleteUser.CannotDeleteSelf", "No puedes eliminar tu propio usuario."));
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        if (user.Role == UserRole.Student)
        {
            return Result.Failure<Unit>(Error.Validation("DeleteUser.NotAStaffUser", "Los alumnos no se eliminan desde este panel."));
        }

        if (user.Role == UserRole.Administrator)
        {
            var activeAdmins = await users.GetActiveAdministratorsAsync(ct);
            if (!activeAdmins.Any(a => a.Id != user.Id))
            {
                return Result.Failure<Unit>(Error.Validation("DeleteUser.LastAdministrator", "No puedes eliminar al último administrador activo."));
            }
        }

        await users.DeleteAsync(command.UserId, ct);
        return Result.Success(Unit.Value);
    }
}
