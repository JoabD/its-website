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

        // BUG REAL encontrado: al declarar "int page, int pageSize" SIN valor por defecto, Minimal
        // API exige que el query string los incluya SIEMPRE — si la petición los omite (como hacían
        // Alumnos y Docentes, que solo mandan role/pageSize o ni eso), ASP.NET Core lanza
        // BadHttpRequestException ANTES de llegar aquí dentro ("Required parameter \"int page\" was
        // not provided"), devolviendo 500 y dejando el fallback "page == 0 ? 1 : page" como código
        // muerto (nunca se ejecuta porque el binding falla primero). Al darles valor por defecto,
        // el parámetro se vuelve opcional para el binder y el fallback sí puede operar.
        group.MapGet("/", async (UserRole? role, string? regionId, string? search, IDispatcher dispatcher, CancellationToken ct, int page = 0, int pageSize = 0) =>
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
