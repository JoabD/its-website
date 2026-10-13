using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Academics.AssignTeacher;
using Shekinah.Application.Academics.AutoEnroll;
using Shekinah.Application.Academics.GetCurrentPeriod;
using Shekinah.Application.Academics.GetGradeImportTemplate;
using Shekinah.Application.Academics.GetGradingRoster;
using Shekinah.Application.Academics.GetOfferingEnrollments;
using Shekinah.Application.Academics.GetOfferings;
using Shekinah.Application.Academics.GetPeriods;
using Shekinah.Application.Academics.GetStudentGradeImportTemplate;
using Shekinah.Application.Academics.GetStudentOfferings;
using Shekinah.Application.Academics.ImportGrades;
using Shekinah.Application.Academics.ImportStudentGrades;
using Shekinah.Application.Academics.OpenPeriod;
using Shekinah.Application.Academics.PromoteStudents;
using Shekinah.Application.Academics.RecordGrade;
using Shekinah.Application.Academics.RecordGradesBatch;
using Shekinah.Application.Academics.RecordStudentGradesBatch;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Api.Endpoints;

public static class AcademicEndpoints
{
    public static void MapAcademicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/academic").WithTags("Académico").RequireAuthorization();

        group.MapGet("/periods", async (int page, int pageSize, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetPeriodsQuery(page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize), ct)).ToApiResult());

        group.MapGet("/periods/current", async (IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetCurrentPeriodQuery(), ct)).ToApiResult());

        group.MapPost("/periods", async (OpenPeriodCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created));

        group.MapGet("/offerings", async (string periodId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetOfferingsQuery(periodId), ct)).ToApiResult());

        group.MapPost("/offerings", async (AssignTeacherCommand command, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(command, ct)).ToApiResult(StatusCodes.Status201Created));

        group.MapDelete("/offerings/{id}", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RemoveOfferingCommand(id), ct)).ToApiResult());

        group.MapPost("/offerings/auto-enroll", async (AutoEnrollRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new AutoEnrollCommand(request.PeriodId), ct)).ToApiResult());

        // "Promover alumnos" (2026-10, pedido explícito del cliente): acción directa, independiente
        // de abrir/cerrar un periodo académico — ver PromoteStudentsCommand.
        group.MapPost("/students/promote", async (PromoteStudentsRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new PromoteStudentsCommand(request.Plan, request.FromTerm), ct)).ToApiResult());

        // "Calificar por alumno": trae todas las materias (ofertas) de un alumno en un periodo, para
        // poblar la pantalla antes de capturar — ver GetStudentOfferingsQuery.
        group.MapGet("/students/{studentId}/offerings", async (string studentId, string periodId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetStudentOfferingsQuery(studentId, periodId), ct)).ToApiResult());

        // "Calificar por alumno" (2026-10, pedido explícito del cliente: captura en lote para TODAS
        // las materias de un alumno a la vez, en vez de ir oferta por oferta) — ver
        // RecordStudentGradesBatchCommand.
        group.MapPut("/offerings/students/{studentId}/grades", async (string studentId, StudentGradesBatchRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RecordStudentGradesBatchCommand(studentId, request.PeriodId, request.Grades), ct)).ToApiResult());

        // Rediseño de "Calificar por alumno" (2026-10, feedback real: el dropdown no servía — no
        // mostraba nada hasta elegir un alumno, y no explicaba por qué alguno salía sin materias).
        // Da, para TODO el periodo, cuántas materias tiene y cuántas ya calificó cada alumno, para
        // pintar la columna "Materias calificadas" de la tabla sin una llamada por alumno.
        group.MapGet("/grading-roster", async (string periodId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetGradingRosterQuery(periodId), ct)).ToApiResult());

        // "Calificar por alumno" → Bulk Assignment por alumno vía Excel (2026-10, pedido explícito
        // del cliente, además del Bulk Assignment por grupo que ya existía arriba).
        group.MapGet("/students/{studentId}/grades/import/template", async (string studentId, string periodId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetStudentGradeImportTemplateQuery(studentId, periodId), ct)).ToApiResult());

        group.MapPost("/students/{studentId}/grades/import", async (string studentId, string periodId, IFormFile file, IDispatcher dispatcher, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var command = new ImportStudentGradesCommand(studentId, periodId, file.FileName, stream);
            return (await dispatcher.SendAsync(command, ct)).ToApiResult();
        }).DisableAntiforgery();

        group.MapGet("/offerings/{id}/enrollments", async (string id, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetOfferingEnrollmentsQuery(id), ct)).ToApiResult());

        group.MapPut("/offerings/{id}/enrollments/{studentId}/grade", async (string id, string studentId, GradeRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RecordGradeCommand(id, studentId, request.Grade), ct)).ToApiResult());

        // Captura manual en lote — "Guardar todos los cambios" de la pantalla de calificaciones
        // (plan de control escolar / Kardex, fase 2): evita una petición por alumno.
        group.MapPut("/offerings/{id}/enrollments/grades", async (string id, GradesBatchRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new RecordGradesBatchCommand(id, request.Grades), ct)).ToApiResult());

        // Importación de calificaciones por Excel — una plantilla cubre TODAS las materias de un
        // (periodo, plan, cuatrimestre/semestre, región) a la vez (plan de control escolar / Kardex,
        // fase 2). Grupo propio (no /offerings/{id}) porque no aplica a un solo grupo sino a todos
        // los de ese cuatrimestre/semestre+región.
        group.MapGet("/grades/import/template", async (string periodId, StudyPlan plan, int termNumber, string regionId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetGradeImportTemplateQuery(periodId, plan, termNumber, regionId), ct)).ToApiResult());

        group.MapPost("/grades/import", async (string periodId, StudyPlan plan, int termNumber, string regionId, IFormFile file, IDispatcher dispatcher, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var command = new ImportGradesCommand(periodId, plan, termNumber, regionId, file.FileName, stream);
            return (await dispatcher.SendAsync(command, ct)).ToApiResult();
        }).DisableAntiforgery();
    }

    public sealed record AutoEnrollRequest(string PeriodId);

    public sealed record GradeRequest(int Grade);

    public sealed record GradesBatchRequest(IReadOnlyList<GradeEntry> Grades);

    public sealed record PromoteStudentsRequest(StudyPlan Plan, int FromTerm);

    public sealed record StudentGradesBatchRequest(string PeriodId, IReadOnlyList<StudentSubjectGradeEntry> Grades);
}
