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

        // RN-11 / RN-16: un profesor solo califica alumnos de SUS asignaciones.
        if (currentUser.Role == UserRole.Teacher && offering.Teacher.Id != currentUser.UserId)
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
