using System.Net;
using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.AdminUpdateUser;

/// <summary>
/// Panel de Usuarios → editar (side panel, mismo patrón visual que Alumnos/Pagos): a diferencia de
/// <see cref="UpdateUser.UpdateUserCommand"/> (que solo toca rol/región/estatus), este comando edita
/// la información personal (nombre/correo/teléfono, RN-23: nunca toca el expediente de admisión) y,
/// opcionalmente, establece una contraseña NUEVA elegida por el admin — a propósito NO temporal ni
/// aleatoria, a diferencia de <see cref="CreateUser.TemporaryPasswordGenerator"/>: el cliente pidió
/// explícitamente poder capturarla él mismo. Por eso <see cref="Domain.Identity.User.ChangePassword"/>
/// (vía <c>Credentials.WithNewPassword</c>) deja <c>MustChangePassword</c> en false — no tendría
/// sentido pedirle "cámbiala" a alguien que acaba de recibir la que el admin escribió a mano.
///
/// RN-12/seguridad: solo Administrator, nunca sobre uno mismo (mismo principio que
/// ResetPasswordCommandHandler/UpdateUserCommandHandler) y nunca sobre un alumno — Alumnos tiene su
/// propio flujo de edición y este panel los excluye a propósito (pedido explícito del cliente).
///
/// Notificación por correo: pedido explícito del cliente, con la contraseña en texto plano en el
/// cuerpo del correo — normalmente impensable, pero el cliente entiende el riesgo y lo acepta "por
/// ahora". Se envía siempre que se guarda una edición (para que el usuario sepa que su información
/// cambió), incluyendo la contraseña solo cuando de verdad se estableció una nueva — la anterior no
/// se puede reenviar nunca: solo vive como hash irreversible.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record AdminUpdateUserCommand(string UserId, string FullName, string Email, string Phone, string? NewPassword) : ICommand<Unit>;

public sealed class AdminUpdateUserCommandValidator : AbstractValidator<AdminUpdateUserCommand>
{
    public AdminUpdateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.NewPassword).MinimumLength(8).WithMessage("La nueva contraseña debe tener al menos 8 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}

public sealed class AdminUpdateUserCommandHandler(
    IUserRepository users, IPasswordHasher passwordHasher, IEmailSender emailSender, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<AdminUpdateUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(AdminUpdateUserCommand command, CancellationToken ct)
    {
        if (currentUser.UserId == command.UserId)
        {
            return Result.Failure<Unit>(Error.Forbidden(
                "AdminUpdateUser.CannotEditSelf", "No puedes editar tu propio usuario desde este panel — usa \"Cambiar contraseña\" en tu menú de usuario."));
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        if (user.Role == UserRole.Student)
        {
            return Result.Failure<Unit>(Error.Validation("AdminUpdateUser.NotAStaffUser", "Los alumnos se editan desde \"Alumnos\", no desde este panel."));
        }

        var name = PersonName.Create(command.FullName);
        var email = Email.Create(command.Email);
        var phone = PhoneNumber.Create(command.Phone);

        if (name.IsFailure) return Result.Failure<Unit>(name.Error);
        if (email.IsFailure) return Result.Failure<Unit>(email.Error);
        if (phone.IsFailure) return Result.Failure<Unit>(phone.Error);

        var existing = await users.GetByEmailAsync(email.Value.Value, ct);
        if (existing is not null && existing.Id != user.Id)
        {
            return Result.Failure<Unit>(Error.Conflict("AdminUpdateUser.EmailInUse", "Ya existe otro usuario con ese correo."));
        }

        var updatedProfile = PersonalProfile.Create(
            name.Value, email.Value, phone.Value, user.Profile.BirthDate, user.Profile.MaritalStatus, user.Profile.Address,
            user.Profile.Church, user.Profile.Education, user.Profile.TheologicalBackground, user.Profile.StudyPurpose);
        if (updatedProfile.IsFailure) return Result.Failure<Unit>(updatedProfile.Error);

        user.UpdateProfile(updatedProfile.Value);

        string? plainNewPassword = null;
        if (!string.IsNullOrWhiteSpace(command.NewPassword))
        {
            plainNewPassword = command.NewPassword;
            var passwordResult = user.ChangePassword(passwordHasher.Hash(command.NewPassword), clock);
            if (passwordResult.IsFailure) return Result.Failure<Unit>(passwordResult.Error);
        }

        await users.UpdateAsync(user, ct);

        var passwordSection = plainNewPassword is not null
            ? $"<p>Tu nueva contraseña: <strong>{WebUtility.HtmlEncode(plainNewPassword)}</strong></p>"
            : string.Empty;

        await emailSender.SendAsync(
            email.Value.Value,
            "Tu cuenta fue actualizada — Instituto Teológico Shekinah",
            $"""
            <p>Hola {WebUtility.HtmlEncode(name.Value.FullName)},</p>
            <p>Se actualizó tu información en el sistema del Instituto Teológico Shekinah.</p>
            <p>Correo: {WebUtility.HtmlEncode(email.Value.Value)}<br>Teléfono: {WebUtility.HtmlEncode(phone.Value.Value)}</p>
            {passwordSection}
            <p style="color:#8994a8;font-size:12px;">Si no reconoces este cambio, contacta a la administración del instituto.</p>
            """,
            ct);

        return Result.Success(Unit.Value);
    }
}
