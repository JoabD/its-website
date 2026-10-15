using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.RecordGrade;

/// <summary>RN-16: solo el profesor dueño de la oferta o un administrador; queda auditada.</summary>
[RequireRole(UserRole.Teacher, UserRole.Administrator)]
public sealed record RecordGradeCommand(string OfferingId, string StudentId, int Grade) : ICommand<Abstractions.Unit>;

public sealed class RecordGradeCommandHandler(
    Domain.Academics.ICourseOfferingRepository offerings, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<RecordGradeCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(RecordGradeCommand command, CancellationToken ct)
    {
        var offering = await offerings.GetByIdAsync(command.OfferingId, ct);
        if (offering is null)
        {
            return Result.Failure<Abstractions.Unit>(Error.NotFound("CourseOffering.NotFound", "Oferta no encontrada."));
        }

        // RN-11 / RN-16, más "oferta pendiente" (2026-10, "no importa por ahora solo un
        // administrador lo hará"): si la oferta todavía no tiene profesor asignado (se creó
        // automáticamente para que el currículo del alumno sea calificable de inmediato — ver
        // EnsureStudentOfferingsCommand), no hay dueño contra quien comparar, así que solo un
        // Administrador puede calificarla. Antes esto se activaba solo "si currentUser.Role ==
        // Teacher" — con roles múltiples (2026-10) eso dejaba un hueco: alguien cuyo rol PRINCIPAL
        // no es Teacher ni Administrator, pero que SÍ tiene Teacher como rol adicional (ver
        // User.ExtraRoles), pasaba [RequireRole(Teacher, Administrator)] por su ExtraRoles pero
        // nunca entraba a este "if" (su Role no es Teacher), así que la validación de dueño se
        // saltaba por completo. Ahora la regla es la correcta en cualquier combinación: solo un
        // Administrador (por rol principal o adicional) puede calificar una oferta que no es suya
        // o que no tiene profesor.
        var isAdministrator = currentUser.Role == UserRole.Administrator || currentUser.ExtraRoles.Contains(UserRole.Administrator);
        if (!isAdministrator && (offering.Teacher is null || offering.Teacher.Id != currentUser.UserId))
        {
            return Result.Failure<Abstractions.Unit>(Error.Forbidden("RecordGrade.NotOwner", "Solo el profesor asignado o un administrador puede calificar esta oferta."));
        }

        var gradeResult = Grade.Create(command.Grade);
        if (gradeResult.IsFailure)
        {
            return Result.Failure<Abstractions.Unit>(gradeResult.Error);
        }

        var result = offering.RecordGrade(command.StudentId, gradeResult.Value, currentUser.UserId ?? "system", clock);
        if (result.IsFailure)
        {
            return Result.Failure<Abstractions.Unit>(result.Error);
        }

        await offerings.UpdateAsync(offering, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
