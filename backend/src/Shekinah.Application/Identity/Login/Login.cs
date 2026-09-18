using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.Login;

/// <summary>RN-07: login por correo electrónico + contraseña. [anónimo] en el contrato §7.</summary>
[AllowAnonymousUseCase]
public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

public sealed record LoginResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, string UserId, UserRole Role, bool MustChangePassword);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

/// <summary>
/// RN-07: se rechaza si no existe, la contraseña no coincide, está inactivo o bloqueado por
/// morosidad. El mensaje de credenciales inválidas NUNCA revela cuál de las dos cosas falló
/// (no revela si el correo existe).
/// </summary>
public sealed class LoginCommandHandler(
    IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokenService, IClock clock)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    private static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Correo o contraseña incorrectos.");

    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(command.Email.Trim().ToLowerInvariant(), ct);
        if (user is null)
        {
            return Result.Failure<LoginResponse>(InvalidCredentials);
        }

        if (user.Credentials.IsTemporarilyLocked(clock))
        {
            return Result.Failure<LoginResponse>(Error.Unauthorized("Auth.TemporarilyLocked", "Demasiados intentos fallidos. Intente más tarde."));
        }

        if (!passwordHasher.Verify(command.Password, user.Credentials.PasswordHash))
        {
            user.RegisterFailedLogin(clock, maxAttempts: 5, lockDuration: TimeSpan.FromMinutes(15));
            await users.UpdateAsync(user, ct);
            return Result.Failure<LoginResponse>(InvalidCredentials);
        }

        if (!user.CanAuthenticate(out var blockReason))
        {
            return Result.Failure<LoginResponse>(Error.Forbidden("Auth.Blocked", blockReason!));
        }

        user.RegisterSuccessfulLogin(clock);
        await users.UpdateAsync(user, ct);

        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user.Id, user.EnrollmentNumber.Value, user.Role, user.Region?.Id);
        var (refreshToken, _) = tokenService.CreateRefreshToken();

        return Result.Success(new LoginResponse(accessToken, expiresAt, refreshToken, user.Id, user.Role, user.Credentials.MustChangePassword));
    }
}
