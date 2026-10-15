using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.PromoteStudent;

/// <summary>
/// "Calificar por alumno" → "Avanzar de cuatrimestre/semestre" (2026-10, pedido explícito del
/// cliente: "debemos habilitar eso de la promoción por alumno"). A diferencia de
/// <see cref="Shekinah.Application.Academics.PromoteStudents.PromoteStudentsCommand"/> (que sube a
/// TODO un plan+término a la vez), esta promueve a UN alumno puntual — pero solo si ya aprobó
/// TODAS sus materias del cuatrimestre/semestre actual (ver <see cref="PromotionEligibilityService"/>).
/// Si no es elegible, no es un error: <see cref="PromoteStudentResponse.Reason"/> explica por qué,
/// para que la UI lo muestre sin romper el flujo.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record PromoteStudentCommand(string StudentId) : ICommand<PromoteStudentResponse>, ITransactionalCommand;

public sealed record PromoteStudentResponse(bool Promoted, bool Graduated, string? Reason);

public sealed class PromoteStudentCommandHandler(
    IUserRepository users, ICourseOfferingRepository offerings, ISubjectRepository subjects,
    IAcademicPeriodRepository periods, IClock clock)
    : ICommandHandler<PromoteStudentCommand, PromoteStudentResponse>
{
    public async Task<Result<PromoteStudentResponse>> HandleAsync(PromoteStudentCommand command, CancellationToken ct)
    {
        var student = await users.GetByIdAsync(command.StudentId, ct);
        if (student is null || student.Role != UserRole.Student || student.Academic is null)
        {
            return Result.Failure<PromoteStudentResponse>(Error.Validation("PromoteStudent.NotAStudent", "El usuario indicado no es un alumno."));
        }

        if (student.Academic.IsGraduated)
        {
            return Result.Success(new PromoteStudentResponse(false, false, "Este alumno ya está egresado."));
        }

        var activePeriod = await periods.GetActiveAsync(ct);
        if (activePeriod is null)
        {
            return Result.Success(new PromoteStudentResponse(false, false, "No hay un periodo académico activo."));
        }

        var (eligible, reason) = await PromotionEligibilityService.EvaluateAsync(student, activePeriod.Id, subjects, offerings, ct);
        if (!eligible)
        {
            return Result.Success(new PromoteStudentResponse(false, false, reason));
        }

        var promoteResult = student.PromoteTerm(clock);
        if (promoteResult.IsFailure)
        {
            return Result.Failure<PromoteStudentResponse>(promoteResult.Error);
        }

        await users.UpdateAsync(student, ct);

        var graduated = student.Academic!.IsGraduated;
        if (!graduated)
        {
            var periodOfferings = await offerings.GetByPeriodAsync(activePeriod.Id, ct);
            foreach (var offering in periodOfferings)
            {
                var enrollResult = CourseEnrollmentService.EnrollEligible(offering, [student], clock);
                if (enrollResult.IsSuccess && enrollResult.Value > 0)
                {
                    await offerings.UpdateAsync(offering, ct);
                }
            }
        }

        return Result.Success(new PromoteStudentResponse(true, graduated, null));
    }
}
