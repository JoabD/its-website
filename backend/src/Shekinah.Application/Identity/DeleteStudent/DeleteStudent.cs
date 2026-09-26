using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.DeleteStudent;

/// <summary>
/// Alumnos → "Eliminar" (pedido explícito del cliente junto con "dar de baja": "una vez dados de
/// baja, que se puedan eliminar"). Espejo de <see cref="DeleteUser.DeleteUserCommand"/>
/// (Panel de Usuarios), pero al revés: aquí SOLO se permite sobre un alumno, y ADEMÁS exige que ya
/// esté dado de baja (Status == Inactive) — un candado explícito para que eliminar sea siempre una
/// acción de dos pasos (primero dar de baja, confirmar que es el alumno correcto, y solo entonces
/// poder borrarlo), nunca un clic accidental sobre un alumno activo. Igual que
/// DeleteUserCommandHandler: borrado físico — los campos que referencian un userId en otras
/// colecciones (pagos, calificaciones, kardex, avisos) son bitácora histórica en Mongo, no llaves
/// foráneas — no hay integridad referencial que romper.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record DeleteStudentCommand(string StudentId) : ICommand<Unit>;

public sealed class DeleteStudentCommandHandler(IUserRepository users) : ICommandHandler<DeleteStudentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteStudentCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.StudentId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Alumno no encontrado."));
        }

        if (user.Role != UserRole.Student)
        {
            return Result.Failure<Unit>(Error.Validation("DeleteStudent.NotAStudent", "El usuario indicado no es un alumno."));
        }

        if (user.Status != UserStatus.Inactive)
        {
            return Result.Failure<Unit>(Error.Validation(
                "DeleteStudent.MustBeInactive", "Primero debes dar de baja al alumno antes de poder eliminarlo."));
        }

        await users.DeleteAsync(command.StudentId, ct);
        return Result.Success(Unit.Value);
    }
}
