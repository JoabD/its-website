using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Identity.ChangePassword;
using Shekinah.Application.Identity.GetCurrentUser;
using Shekinah.Application.Identity.Login;
using Shekinah.Application.Identity.RefreshToken;

namespace Shekinah.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult()).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult()).AllowAnonymous();

        group.MapPost("/logout", () => Results.NoContent()).RequireAuthorization();

        group.MapPost("/change-password", async (ChangePasswordRequest request, ICurrentUser currentUser, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var command = new ChangePasswordCommand(currentUser.UserId!, request.CurrentPassword, request.NewPassword);
            return (await dispatcher.SendAsync(command, ct)).ToApiResult();
        }).RequireAuthorization();

        group.MapGet("/me", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetCurrentUserQuery(), ct)).ToApiResult()).RequireAuthorization();
    }

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
