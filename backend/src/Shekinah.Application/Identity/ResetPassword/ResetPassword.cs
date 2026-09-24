using System.Net;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Identity.CreateUser;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.ResetPassword;

[RequireRole(UserRole.Administrator)]
public sealed record ResetPasswordCommand(string UserId) : ICommand<ResetPasswordResponse>;

public sealed record ResetPasswordResponse(string TemporaryPassword);

public sealed class ResetPasswordCommandHandler(IUserRepository users, IPasswordHasher passwordHasher, IEmailSender emailSender, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    public async Task<Result<ResetPasswordResponse>> HandleAsync(ResetPasswordCommand command, CancellationToken ct)
    {
        // Pedido explícito del cliente: el usuario en sesión no puede editarse/restablecerse su
        // propia contraseña desde el panel de administración de usuarios — para eso ya existe el
        // flujo propio "Cambiar contraseña" (ChangePasswordCommand, que sí pide la contraseña actual).
        // Sin este chequeo, un Administrator podría resetear su propia cuenta sin conocer la
        // contraseña vigente, saltándose esa verificación.
        if (currentUser.UserId == command.UserId)
        {
            return Result.Failure<ResetPasswordResponse>(Error.Forbidden(
                "ResetPassword.CannotResetSelf", "No puedes restablecer tu propia contraseña desde aquí — usa \"Cambiar contraseña\" en tu menú de usuario."));
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<ResetPasswordResponse>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        user.ResetPassword(passwordHasher.Hash(temporaryPassword), currentUser.UserId ?? "system", clock);
        await users.UpdateAsync(user, ct);

        // Mismo principio de notificación que AdminUpdateUserCommand/CreateUserCommand: quien cambia
        // de contraseña se entera por correo, con la contraseña en texto plano (pedido explícito del
        // cliente). Esta sí es temporal (RN-05/24: user.ResetPassword deja MustChangePassword=true).
        await emailSender.SendAsync(
            user.Profile.Email.Value,
            "Tu contraseña fue restablecida — Instituto Teológico Shekinah",
            $"""
            <p>Hola {WebUtility.HtmlEncode(user.Profile.FullName.FullName)},</p>
            <p>Un administrador restableció tu contraseña.</p>
            <p>Contraseña temporal: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>
            <p>Deberás cambiarla en tu próximo inicio de sesión.</p>
            <p style="color:#8994a8;font-size:12px;">Si no reconoces este cambio, contacta a la administración del instituto.</p>
            """,
            ct);

        return Result.Success(new ResetPasswordResponse(temporaryPassword));
    }
}
