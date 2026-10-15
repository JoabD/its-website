using Shekinah.Application.Abstractions;
using Shekinah.Application.Academics.GetGradeImportTemplate;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.GetStudentGradeImportTemplate;

/// <summary>
/// "Calificar por alumno" → "Importar calificaciones de este alumno (Excel)" (2026-10, pedido
/// explícito del cliente: Bulk Assignment tanto por grupo — ya existía, ver
/// GetGradeImportTemplateQuery — como por alumno individual). Reutiliza el mismo
/// <see cref="ISpreadsheetWriter.BuildGradeImportTemplate"/> que la plantilla por grupo, pero con
/// un solo alumno y SOLO las materias en las que ya está inscrito (no todo el catálogo del
/// cuatrimestre/semestre) — así la plantilla siempre tiene algo que llenar.
/// </summary>
[RequireRole(UserRole.Teacher, UserRole.Administrator)]
public sealed record GetStudentGradeImportTemplateQuery(string StudentId, string PeriodId) : IQuery<GradeImportTemplateResponse>;

public sealed class GetStudentGradeImportTemplateQueryHandler(
    Domain.Academics.ICourseOfferingRepository offerings, Domain.Identity.IUserRepository users, ISpreadsheetWriter writer, ICurrentUser currentUser)
    : IQueryHandler<GetStudentGradeImportTemplateQuery, GradeImportTemplateResponse>
{
    public async Task<Result<GradeImportTemplateResponse>> HandleAsync(GetStudentGradeImportTemplateQuery query, CancellationToken ct)
    {
        var studentOfferings = await offerings.GetByStudentAndPeriodAsync(query.StudentId, query.PeriodId, ct);
        if (studentOfferings.Count == 0)
        {
            return Result.Failure<GradeImportTemplateResponse>(Error.Validation(
                "GetStudentGradeImportTemplate.NoSubjects", "Este alumno no tiene materias asignadas en el periodo activo."));
        }

        var isAdministrator = currentUser.Role == UserRole.Administrator || currentUser.ExtraRoles.Contains(UserRole.Administrator);
        if (!isAdministrator && studentOfferings.All(o => o.Teacher?.Id != currentUser.UserId))
        {
            return Result.Failure<GradeImportTemplateResponse>(Error.Forbidden(
                "GetStudentGradeImportTemplate.NotOwner", "No tienes ninguna materia asignada a este alumno."));
        }

        var student = await users.GetByIdAsync(query.StudentId, ct);
        if (student is null)
        {
            return Result.Failure<GradeImportTemplateResponse>(Error.NotFound("User.NotFound", "Alumno no encontrado."));
        }

        var planLabel = student.Academic?.Plan == StudyPlan.Semester ? "Semestral" : "Cuatrimestral";
        var termNumber = student.Academic?.CurrentTerm.Value ?? 0;
        var regionName = student.Region?.Name ?? "";

        var subjectColumns = studentOfferings.Select(o => new GradeImportTemplateSubject(o.Subject.Name)).ToList();
        var templateStudent = new GradeImportTemplateStudent(student.EnrollmentNumber.Value, student.Profile.FullName.FullName);

        var bytes = writer.BuildGradeImportTemplate("Periodo activo", planLabel, termNumber, regionName, subjectColumns, [templateStudent]);
        var fileName = $"Plantilla_Calificaciones_{student.Profile.FullName.FullName.Replace(' ', '_')}.xlsx";

        return Result.Success(new GradeImportTemplateResponse(fileName, Convert.ToBase64String(bytes)));
    }
}
