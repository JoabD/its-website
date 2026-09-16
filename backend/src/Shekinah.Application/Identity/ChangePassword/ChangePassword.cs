using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Identity.ChangePassword;

/// <summary>RN-24: obligatorio en el primer login o tras un reseteo. Ningún otro endpoint debe responder hasta que ocurra
/// (esa restricción global se aplica en el middleware/guard de la capa Api, no aquí).</summary>
public sealed record ChangePasswordCommand(string UserId, string CurrentPassword, string NewPassword) : ICommand<Unit>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).MinimumLength(12).WithMessage("La nueva contraseña debe tener al menos 12 caracteres.");
        RuleFor(x => x.NewPassword).Matches("[A-Za-z]").Matches("[0-9]").WithMessage("La nueva contraseña debe ser alfanumérica.");
    }
}

public sealed class ChangePasswordCommandHandler(Domain.Identity.IUserRepository users, IPasswordHasher passwordHasher, Domain.Common.IClock clock)
    : ICommandHandler<ChangePasswordCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(ChangePasswordCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        if (!passwordHasher.Verify(command.CurrentPassword, user.Credentials.PasswordHash))
        {
            return Result.Failure<Unit>(Error.Validation("ChangePassword.WrongCurrent", "La contraseña actual no es correcta."));
        }

        var result = user.ChangePassword(passwordHasher.Hash(command.NewPassword), clock);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await users.UpdateAsync(user, ct);
        return Result.Success(Unit.Value);
    }
}
