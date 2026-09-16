using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.AssignTeacher;

/// <summary>RN-22: una asignación docente es única por (periodo, materia, región).</summary>
[RequireRole(UserRole.Administrator)]
public sealed record AssignTeacherCommand(string PeriodId, string SubjectId, string RegionId, string TeacherId) : ICommand<string>;

public sealed class AssignTeacherCommandHandler(
    ICourseOfferingRepository offerings, IAcademicPeriodRepository periods, Domain.Catalog.ISubjectRepository subjects,
    Domain.Catalog.IRegionRepository regions, IUserRepository users)
    : ICommandHandler<AssignTeacherCommand, string>
{
    public async Task<Result<string>> HandleAsync(AssignTeacherCommand command, CancellationToken ct)
    {
        var existing = await offerings.FindAsync(command.PeriodId, command.SubjectId, command.RegionId, ct);
        if (existing is not null)
        {
            return Result.Failure<string>(Error.Conflict("CourseOffering.Duplicate", "Ya existe una asignación para esa materia/región en este periodo (RN-22)."));
        }

        var period = await periods.GetByIdAsync(command.PeriodId, ct);
        var subject = await subjects.GetByIdAsync(command.SubjectId, ct);
        var region = await regions.GetByIdAsync(command.RegionId, ct);
        var teacher = await users.GetByIdAsync(command.TeacherId, ct);

        if (period is null || subject is null || region is null || teacher is null)
        {
            return Result.Failure<string>(Error.Validation("AssignTeacher.InvalidReference", "Periodo, materia, región o profesor no válidos."));
        }

        if (teacher.Role != UserRole.Teacher)
        {
            return Result.Failure<string>(Error.Validation("AssignTeacher.NotATeacher", "El usuario indicado no tiene rol de profesor."));
        }

        var offeringResult = CourseOffering.Create(
            EntityId.NewId(),
            new PeriodRef(period.Id, period.Code.Value),
            new SubjectRef(subject.Id, subject.Name, subject.TermNumber?.Value),
            new RegionRefAcademics(region.Id, region.LegacyCode, region.Name),
            new TeacherRef(teacher.Id, teacher.EnrollmentNumber.Value, teacher.Profile.FullName.FullName));

        if (offeringResult.IsFailure) return Result.Failure<string>(offeringResult.Error);

        await offerings.AddAsync(offeringResult.Value, ct);
        return Result.Success(offeringResult.Value.Id);
    }
}

[RequireRole(UserRole.Administrator)]
public sealed record RemoveOfferingCommand(string OfferingId) : ICommand<Abstractions.Unit>;

public sealed class RemoveOfferingCommandHandler(ICourseOfferingRepository offerings) : ICommandHandler<RemoveOfferingCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(RemoveOfferingCommand command, CancellationToken ct)
    {
        await offerings.DeleteAsync(command.OfferingId, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
