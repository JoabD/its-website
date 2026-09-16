using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;

namespace Shekinah.Application.Identity.RefreshToken;

/// <summary>
/// Refresh rotativo (7 días). La validación del hash del refresh token contra <c>refreshTokens</c>
/// vive en Infrastructure (colección con TTL, spec técnico §5.2); aquí solo se orquesta.
/// </summary>
[AllowAnonymousUseCase]
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResponse>;

public sealed record RefreshTokenResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);

public interface IRefreshTokenStore
{
    Task<string?> GetUserIdForValidTokenAsync(string refreshToken, CancellationToken ct);

    Task RevokeAsync(string refreshToken, string? replacedByHash, CancellationToken ct);

    Task StoreAsync(string userId, string refreshTokenHash, DateTime expiresAtUtc, CancellationToken ct);
}

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenStore store, Domain.Identity.IUserRepository users, ITokenService tokenService)
    : ICommandHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    public async Task<Result<RefreshTokenResponse>> HandleAsync(RefreshTokenCommand command, CancellationToken ct)
    {
        var userId = await store.GetUserIdForValidTokenAsync(command.RefreshToken, ct);
        if (userId is null)
        {
            return Result.Failure<RefreshTokenResponse>(Error.Unauthorized("Auth.InvalidRefreshToken", "El refresh token no es válido o expiró."));
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.CanAuthenticate(out _))
        {
            return Result.Failure<RefreshTokenResponse>(Error.Unauthorized("Auth.InvalidRefreshToken", "El refresh token no es válido o expiró."));
        }

        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user.Id, user.EnrollmentNumber.Value, user.Role, user.Region?.Id);
        var (newRefreshToken, refreshExpiresAt) = tokenService.CreateRefreshToken();

        await store.RevokeAsync(command.RefreshToken, newRefreshToken, ct);
        await store.StoreAsync(user.Id, newRefreshToken, refreshExpiresAt, ct);

        return Result.Success(new RefreshTokenResponse(accessToken, expiresAt, newRefreshToken));
    }
}
