using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.EnsureStudentOfferings;

/// <summary>
/// "Calificar por alumno" (2026-10, pedido explícito del cliente: "no importa por ahora solo un
/// administrador lo hará... necesitamos que se pueda calificar a los alumnos"). Antes, una materia
/// del currículo del alumno sin profesor asignado simplemente no se podía calificar. Ahora, al
/// abrir la ficha de un alumno, esto crea (de forma idempotente) una "oferta pendiente" — sin
/// profesor, ver <see cref="CourseOffering.CreatePending"/> — para cada materia de su currículo
/// que todavía no tenga oferta en el periodo dado, y lo inscribe en ella. Con eso el campo de
/// calificación queda habilitado de inmediato (solo para un Administrador — ver
/// RecordGradeCommandHandler y análogos). Si más adelante se quiere un profesor real,
/// AssignTeacherCommand completa esta misma oferta en vez de crear una duplicada.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record EnsureStudentOfferingsCommand(string StudentId, string PeriodId) : ICommand<int>, ITransactionalCommand;

public sealed class EnsureStudentOfferingsCommandHandler(
    IUserRepository users, ICourseOfferingRepository offerings, ISubjectRepository subjects,
    IAcademicPeriodRepository periods, IClock clock)
    : ICommandHandler<EnsureStudentOfferingsCommand, int>
{
    public async Task<Result<int>> HandleAsync(EnsureStudentOfferingsCommand command, CancellationToken ct)
    {
        var student = await users.GetByIdAsync(command.StudentId, ct);
        if (student is null || student.Role != UserRole.Student || student.Academic is null || student.Academic.IsGraduated || student.Region is null)
        {
            return Result.Success(0);
        }

        var period = await periods.GetByIdAsync(command.PeriodId, ct);
        if (period is null)
        {
            return Result.Failure<int>(Error.NotFound("AcademicPeriod.NotFound", "Periodo no encontrado."));
        }

        var curriculum = await subjects.GetByTermAndPlanAsync(student.Academic.CurrentTerm, student.Academic.Plan, ct);
        var created = 0;

        foreach (var subject in curriculum)
        {
            var offering = await offerings.FindAsync(command.PeriodId, subject.Id, student.Region.Id, ct);
            if (offering is null)
            {
                var offeringResult = CourseOffering.CreatePending(
                    EntityId.NewId(),
                    new PeriodRef(period.Id, period.Code.Value),
                    new SubjectRef(subject.Id, subject.Name, subject.TermNumber?.Value),
                    new RegionRefAcademics(student.Region.Id, student.Region.Code, student.Region.Name));

                if (offeringResult.IsFailure)
                {
                    return Result.Failure<int>(offeringResult.Error);
                }

                offering = offeringResult.Value;
                await offerings.AddAsync(offering, ct);
                created++;
            }

            if (offering.Enrollments.All(e => e.StudentId != student.Id))
            {
                var enrollResult = offering.Enroll(student.Id, student.EnrollmentNumber.Value, student.Profile.FullName.FullName, student.Academic.CurrentTerm, clock);
                if (enrollResult.IsFailure)
                {
                    return Result.Failure<int>(enrollResult.Error);
                }

                await offerings.UpdateAsync(offering, ct);
            }
        }

        return Result.Success(created);
    }
}
