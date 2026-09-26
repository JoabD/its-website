using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Academics.GetMyCourses;
using Shekinah.Application.Identity.CreateStudent;
using Shekinah.Application.Identity.DeleteStudent;
using Shekinah.Application.Identity.GetCurrentUser;
using Shekinah.Application.Identity.GetStudentImportBatch;
using Shekinah.Application.Identity.GetStudentImportTemplate;
using Shekinah.Application.Identity.ImportStudents;
using Shekinah.Application.Identity.SetStudentStatus;
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

        // "Agregar alumno" → pestaña Excel → "Descargar plantilla" (antes solo se mencionaba en
        // texto, sin ningún lugar de dónde bajarla). Va ANTES de /import/{batchId} en este archivo
        // solo por orden de lectura — con rutas literales como esta, el orden de registro no importa
        // para el matching (no compite con el parámetro {batchId}).
        adminGroup.MapGet("/import/template", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetStudentImportTemplateQuery(), ct)).ToApiResult());

        // Alumnos → "Dar de baja" / "Reactivar" y "Eliminar" (pedido explícito del cliente, 2026-09:
        // "necesitamos un mecanismo para dar de baja alumnos, y una vez dados de baja, que se puedan
        // eliminar"). Mismo patrón de rutas que UsersEndpoints (POST /{id}/status, DELETE /{id}),
        // pero con comandos propios de Alumnos — ver SetStudentStatus.cs/DeleteStudent.cs para el
        // porqué de tenerlos separados de SetUserStatusCommand/DeleteUserCommand.
        adminGroup.MapPost("/{id}/status", async (string id, SetStudentStatusRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SetStudentStatusCommand(id, request.Active), ct)).ToApiResult());

        adminGroup.MapDelete("/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new DeleteStudentCommand(id), ct)).ToApiResult());
    }

    public sealed record CreateStudentRequest(
        string FullName, string Email, string Phone, DateOnly BirthDate, string RegionId,
        Modality Modality, StudyPlan Plan, int CurrentTerm);

    public sealed record SetStudentStatusRequest(bool Active);
}
