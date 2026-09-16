using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Academics.GetMyCourses;
using Shekinah.Application.Identity.GetCurrentUser;
using Shekinah.Application.Identity.UpdateMyProfile;

namespace Shekinah.Api.Endpoints;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/students/me").WithTags("Alumno").RequireAuthorization();

        group.MapGet("/courses", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetMyCoursesQuery(), ct)).ToApiResult());

        group.MapGet("/profile", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetCurrentUserQuery(), ct)).ToApiResult());

        group.MapPut("/profile", async (UpdateMyProfileCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult());
    }
}
