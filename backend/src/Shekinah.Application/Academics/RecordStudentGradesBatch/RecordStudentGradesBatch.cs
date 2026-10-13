using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.RecordStudentGradesBatch;

/// <summary>
/// "Calificar por alumno" (2026-10, pedido explícito del cliente: "una por una en cada alumno, o
/// un Bulk assignation por alumno"). A diferencia de <see cref="RecordGradesBatch.RecordGradesBatchCommand"/>
/// (que captura MUCHOS ALUMNOS en UNA oferta), este captura TODAS LAS OFERTAS de UN SOLO alumno en
/// un periodo — las dos direcciones del mismo lote, sin tocar <see cref="CourseOffering.RecordGrade"/>
/// (RN-16), que ya es por-oferta y no necesita cambiar. Igual que el resto de los lotes del sistema:
/// una fila mala no tumba a las demás.
/// </summary>
[RequireRole(UserRole.Teacher, UserRole.Administrator)]
public sealed record RecordStudentGradesBatchCommand(string StudentId, string PeriodId, IReadOnlyList<StudentSubjectGradeEntry> Grades)
    : ICommand<RecordStudentGradesBatchResponse>;

public sealed record StudentSubjectGradeEntry(string OfferingId, int Grade);

public sealed record StudentSubjectGradeError(string OfferingId, string SubjectName, string Code, string Message);

public sealed record RecordStudentGradesBatchResponse(int Saved, IReadOnlyList<StudentSubjectGradeError> Errors);

public sealed class RecordStudentGradesBatchCommandHandler(
    Domain.Academics.ICourseOfferingRepository offerings, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<RecordStudentGradesBatchCommand, RecordStudentGradesBatchResponse>
{
    public async Task<Result<RecordStudentGradesBatchResponse>> HandleAsync(RecordStudentGradesBatchCommand command, CancellationToken ct)
    {
        // RN-11/RN-16, misma regla que RecordGrade — ver nota ahí sobre por qué se verifica
        // "es Administrador" (principal o adicional) en vez de "no es Teacher": con roles múltiples
        // (2026-10) alguien con Teacher solo como rol adicional no debe poder calificar fuera de lo
        // suyo, aunque su rol principal no sea Teacher.
        var isAdministrator = currentUser.Role == UserRole.Administrator || currentUser.ExtraRoles.Contains(UserRole.Administrator);

        var errors = new List<StudentSubjectGradeError>();
        var saved = 0;
        var gradedBy = currentUser.UserId ?? "system";
        var touchedOfferings = new Dictionary<string, Domain.Academics.CourseOffering>();

        foreach (var entry in command.Grades)
        {
            var offering = await offerings.GetByIdAsync(entry.OfferingId, ct);
            if (offering is null)
            {
                errors.Add(new StudentSubjectGradeError(entry.OfferingId, "-", "OFERTA_NO_ENCONTRADA", "Esta materia ya no existe."));
                continue;
            }

            if (!isAdministrator && offering.Teacher.Id != currentUser.UserId)
            {
                errors.Add(new StudentSubjectGradeError(entry.OfferingId, offering.Subject.Name, "NO_ES_DUEÑO", "Solo el profesor asignado o un administrador puede calificar esta materia."));
                continue;
            }

            var gradeResult = Grade.Create(entry.Grade);
            if (gradeResult.IsFailure)
            {
                errors.Add(new StudentSubjectGradeError(entry.OfferingId, offering.Subject.Name, "CALIF_FUERA_DE_RANGO", gradeResult.Error.Message));
                continue;
            }

            var recordResult = offering.RecordGrade(command.StudentId, gradeResult.Value, gradedBy, clock);
            if (recordResult.IsFailure)
            {
                errors.Add(new StudentSubjectGradeError(entry.OfferingId, offering.Subject.Name, "CALIFICACION_RECHAZADA", recordResult.Error.Message));
                continue;
            }

            touchedOfferings.TryAdd(offering.Id, offering);
            saved++;
        }

        foreach (var offering in touchedOfferings.Values)
        {
            await offerings.UpdateAsync(offering, ct);
        }

        return Result.Success(new RecordStudentGradesBatchResponse(saved, errors));
    }
}
