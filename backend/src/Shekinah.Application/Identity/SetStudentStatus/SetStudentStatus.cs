using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.SetStudentStatus;

/// <summary>
/// Alumnos → "Dar de baja" / "Reactivar" (pedido explícito del cliente, 2026-09: "necesitamos un
/// mecanismo para dar de baja alumnos, y una vez dados de baja, que se puedan eliminar"). Mismo
/// patrón que <see cref="SetUserStatus.SetUserStatusCommand"/> (Panel de Usuarios), pero en
/// espejo: aquí SOLO se permite sobre un alumno (Role == Student) — la administración de staff vive
/// en /users y excluye explícitamente a Student (ver SetUserStatusCommandHandler). "Dar de baja"
/// reutiliza el mismo Status que ya existe en el dominio (User.Deactivate()/Reactivate(),
/// UserStatus.Active/Inactive) — no se modela un estatus aparte solo para alumnos.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record SetStudentStatusCommand(string StudentId, bool Active) : ICommand<Unit>;

public sealed class SetStudentStatusCommandHandler(IUserRepository users) : ICommandHandler<SetStudentStatusCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(SetStudentStatusCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.StudentId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Alumno no encontrado."));
        }

        if (user.Role != UserRole.Student)
        {
            return Result.Failure<Unit>(Error.Validation("SetStudentStatus.NotAStudent", "El usuario indicado no es un alumno."));
        }

        if (command.Active) user.Reactivate(); else user.Deactivate();

        await users.UpdateAsync(user, ct);
        return Result.Success(Unit.Value);
    }
}
