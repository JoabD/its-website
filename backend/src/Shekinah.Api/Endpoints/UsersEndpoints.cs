using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Identity.CreateUser;
using Shekinah.Application.Identity.GetUsers;
using Shekinah.Application.Identity.ResetPassword;
using Shekinah.Application.Identity.UpdateUser;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Api.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Usuarios").RequireAuthorization();

        group.MapGet("/", async (UserRole? role, string? regionId, string? search, int page, int pageSize, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetUsersQuery(role, regionId, search, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToApiResult());

        group.MapPost("/", async (CreateUserCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created));

        group.MapPut("/{id}", async (string id, UpdateUserRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new UpdateUserCommand(id, request.Role, request.RegionId, request.Modality, request.Status), ct)).ToApiResult());

        group.MapPost("/{id}/reset-password", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new ResetPasswordCommand(id), ct)).ToApiResult());
    }

    public sealed record UpdateUserRequest(UserRole Role, string? RegionId, Modality? Modality, string? Status);
}
