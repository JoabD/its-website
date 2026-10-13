using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Academics.Policies;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.PromoteStudents;

/// <summary>
/// "Promover alumnos" (2026-10, pedido explícito del cliente: "necesito que hagamos un mecanismo,
/// para que el admin vaya promoviendo a los alumnos de semestre o cuatrimestre según sea, y
/// automáticamente se vayan asignando las materias"). Sube de cuatrimestre/semestre a TODOS los
/// alumnos activos de un plan y término puntuales (todas las regiones a la vez — ver
/// <see cref="Identity.IUserRepository.GetActiveStudentsByPlanAndTermAsync"/>), y a los que no se
/// gradúan los matricula automáticamente en las ofertas ya creadas del periodo activo que les
/// correspondan, reusando <see cref="CourseEnrollmentService"/> (la misma lógica que ya usa el botón
/// "Inscribir ahora" / <c>AutoEnrollCommand</c>).
///
/// A propósito es independiente de <c>OpenPeriodCommand</c> (que promueve SOLO a quien tenía
/// inscripciones en el periodo que se está cerrando, como parte de cerrar/abrir un periodo
/// académico completo): esta es una acción directa que el admin dispara cuando quiere, sin tener
/// que manejar el concepto de "periodo académico" — el cliente pidió simplificar justo eso.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record PromoteStudentsCommand(StudyPlan Plan, int FromTerm) : ICommand<PromoteStudentsResponse>, ITransactionalCommand;

public sealed record PromoteStudentsResponse(int StudentsPromoted, int StudentsGraduated, int EnrollmentsCreated);

public sealed class PromoteStudentsCommandValidator : AbstractValidator<PromoteStudentsCommand>
{
    public PromoteStudentsCommandValidator()
    {
        RuleFor(x => x.FromTerm).InclusiveBetween(1, 6).WithMessage("El cuatrimestre/semestre de origen debe estar entre 1 y 6.");
    }
}

public sealed class PromoteStudentsCommandHandler(
    IUserRepository users, ICourseOfferingRepository offerings, IAcademicPeriodRepository periods, IClock clock)
    : ICommandHandler<PromoteStudentsCommand, PromoteStudentsResponse>
{
    public async Task<Result<PromoteStudentsResponse>> HandleAsync(PromoteStudentsCommand command, CancellationToken ct)
    {
        var fromTermResult = TermNumber.Create(command.FromTerm);
        if (fromTermResult.IsFailure)
        {
            return Result.Failure<PromoteStudentsResponse>(fromTermResult.Error);
        }

        var students = await users.GetActiveStudentsByPlanAndTermAsync(command.Plan, fromTermResult.Value, ct);
        if (students.Count == 0)
        {
            return Result.Success(new PromoteStudentsResponse(0, 0, 0));
        }

        // Misma política de dominio que OpenPeriodCommandHandler usa al cerrar un periodo — aquí
        // se dispara directamente, sin esperar a que el admin cierre/abra un periodo académico.
        var promotionResults = TermPromotionPolicy.PromoteAll(students, clock);
        if (promotionResults.Any(r => r.IsFailure))
        {
            return Result.Failure<PromoteStudentsResponse>(Error.Failure("PromoteStudents.Failed", "No se pudo promover a uno o más alumnos."));
        }

        foreach (var student in students)
        {
            await users.UpdateAsync(student, ct);
        }

        var graduated = students.Count(s => s.Academic!.IsGraduated);
        var stillStudying = students.Where(s => !s.Academic!.IsGraduated).ToList();

        var enrollmentsCreated = 0;
        var activePeriod = await periods.GetActiveAsync(ct);

        // Si no hay periodo activo, o nadie quedó como alumno (todos se graduaron), no hay nada que
        // matricular — esto no es un error: la promoción en sí ya se guardó arriba.
        if (activePeriod is not null && stillStudying.Count > 0)
        {
            var periodOfferings = await offerings.GetByPeriodAsync(activePeriod.Id, ct);
            foreach (var offering in periodOfferings)
            {
                var enrollResult = CourseEnrollmentService.EnrollEligible(offering, stillStudying, clock);
                if (enrollResult.IsFailure)
                {
                    return Result.Failure<PromoteStudentsResponse>(enrollResult.Error);
                }

                if (enrollResult.Value > 0)
                {
                    await offerings.UpdateAsync(offering, ct);
                    enrollmentsCreated += enrollResult.Value;
                }
            }
        }

        return Result.Success(new PromoteStudentsResponse(students.Count, graduated, enrollmentsCreated));
    }
}
