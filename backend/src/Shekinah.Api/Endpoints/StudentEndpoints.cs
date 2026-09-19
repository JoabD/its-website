using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Academics.GetMyCourses;
using Shekinah.Application.Identity.CreateStudent;
using Shekinah.Application.Identity.GetCurrentUser;
using Shekinah.Application.Identity.GetStudentImportBatch;
using Shekinah.Application.Identity.ImportStudents;
using Shekinah.Application.Identity.UpdateMyProfile;
using Shekinah.Domain.SharedKernel;

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

        // Alta manual de alumnos (Alumnos → "Agregar alumno"): formulario directo o Excel — ver
        // CreateStudent.cs / ImportStudents.cs para las RN completas. Grupo propio (no /users) porque
        // es específico de Student (plan, cuatrimestre/semestre), a diferencia de UsersEndpoints que
        // sirve altas genéricas de staff.
        var adminGroup = app.MapGroup("/api/v1/students").WithTags("Alumnos").RequireAuthorization();

        adminGroup.MapPost("/", async (CreateStudentRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(
                new CreateStudentCommand(
                    request.FullName, request.Email, request.Phone, request.BirthDate, request.RegionId,
                    request.Modality, request.Plan, request.CurrentTerm),
                ct)).ToApiResult(StatusCodes.Status201Created));

        adminGroup.MapPost("/import", async (IFormFile file, IDispatcher dispatcher, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var command = new ImportStudentsCommand(file.FileName, stream);
            return (await dispatcher.SendAsync(command, ct)).ToApiResult();
        }).DisableAntiforgery();

        adminGroup.MapGet("/import/{batchId}", async (string batchId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetStudentImportBatchQuery(batchId), ct)).ToApiResult());
    }

    public sealed record CreateStudentRequest(
        string FullName, string Email, string Phone, DateOnly BirthDate, string RegionId,
        Modality Modality, StudyPlan Plan, int CurrentTerm);
}
