using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.RecordGradesBatch;

/// <summary>
/// Captura manual de calificaciones "Guardar todos los cambios": guarda en un solo lote todas las
/// calificaciones capturadas a mano en la pantalla de un grupo, en vez de una petición por alumno
/// (plan de control escolar / Kardex, fase 2). Una fila mala no tumba a las demás — mismo criterio
/// que las importaciones por Excel.
/// </summary>
[RequireRole(UserRole.Teacher, UserRole.Administrator)]
public sealed record RecordGradesBatchCommand(string OfferingId, IReadOnlyList<GradeEntry> Grades) : ICommand<RecordGradesBatchResponse>;

public sealed record GradeEntry(string StudentId, int Grade);

public sealed record GradeEntryError(string StudentId, string Code, string Message);

public sealed record RecordGradesBatchResponse(int Saved, IReadOnlyList<GradeEntryError> Errors);

public sealed class RecordGradesBatchCommandHandler(
    Domain.Academics.ICourseOfferingRepository offerings, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<RecordGradesBatchCommand, RecordGradesBatchResponse>
{
    public async Task<Result<RecordGradesBatchResponse>> HandleAsync(RecordGradesBatchCommand command, CancellationToken ct)
    {
        var offering = await offerings.GetByIdAsync(command.OfferingId, ct);
        if (offering is null)
        {
            return Result.Failure<RecordGradesBatchResponse>(Error.NotFound("CourseOffering.NotFound", "Oferta no encontrada."));
        }

        // RN-11/RN-16, mismo ajuste que RecordGradeCommandHandler (2026-10, "un usuario puede tener
        // múltiples roles"): "es Administrador" (principal o adicional) en vez de "no es Teacher" —
        // de lo contrario, alguien con Teacher solo como rol ADICIONAL (ver User.ExtraRoles) se
        // saltaba esta validación por completo, porque su Role principal nunca era Teacher.
        // "Oferta pendiente" (2026-10, sin profesor asignado todavía): solo un Administrador puede
        // calificarla.
        var isAdministrator = currentUser.Role == UserRole.Administrator || currentUser.ExtraRoles.Contains(UserRole.Administrator);
        if (!isAdministrator && (offering.Teacher is null || offering.Teacher.Id != currentUser.UserId))
        {
            return Result.Failure<RecordGradesBatchResponse>(Error.Forbidden("RecordGradesBatch.NotOwner", "Solo el profesor asignado o un administrador puede calificar esta oferta."));
        }

        var errors = new List<GradeEntryError>();
        var saved = 0;
        var gradedBy = currentUser.UserId ?? "system";

        foreach (var entry in command.Grades)
        {
            var gradeResult = Grade.Create(entry.Grade);
            if (gradeResult.IsFailure)
            {
                errors.Add(new GradeEntryError(entry.StudentId, "INVALID_GRADE", gradeResult.Error.Message));
                continue;
            }

            var result = offering.RecordGrade(entry.StudentId, gradeResult.Value, gradedBy, clock);
            if (result.IsFailure)
            {
                errors.Add(new GradeEntryError(entry.StudentId, "RECORD_FAILED", result.Error.Message));
                continue;
            }

            saved++;
        }

        if (saved > 0)
        {
            await offerings.UpdateAsync(offering, ct);
        }

        return Result.Success(new RecordGradesBatchResponse(saved, errors));
    }
}
