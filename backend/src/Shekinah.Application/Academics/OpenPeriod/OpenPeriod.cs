using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Academics.Policies;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.OpenPeriod;

/// <summary>
/// RN-14, en una sola transacción: (a) promueve el cuatrimestre de los alumnos activos con
/// inscripciones en el periodo que se cierra, (b) cierra el periodo activo, (c) crea y activa el
/// nuevo. Si algo falla, no cambia nada (ITransactionalCommand).
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record OpenPeriodCommand(string Code, string Name, DateTime StartsOnUtc, DateTime EndsOnUtc) : ICommand<string>, ITransactionalCommand;

public sealed class OpenPeriodCommandValidator : AbstractValidator<OpenPeriodCommand>
{
    public OpenPeriodCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.EndsOnUtc).GreaterThanOrEqualTo(x => x.StartsOnUtc);
    }
}

public sealed class OpenPeriodCommandHandler(
    IAcademicPeriodRepository periods, ICourseOfferingRepository offerings, IUserRepository users, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<OpenPeriodCommand, string>
{
    public async Task<Result<string>> HandleAsync(OpenPeriodCommand command, CancellationToken ct)
    {
        var codeResult = PeriodCode.Create(command.Code);
        if (codeResult.IsFailure) return Result.Failure<string>(codeResult.Error);

        var dateRangeResult = DateRange.Create(command.StartsOnUtc, command.EndsOnUtc);
        if (dateRangeResult.IsFailure) return Result.Failure<string>(dateRangeResult.Error);

        var currentActive = await periods.GetActiveAsync(ct);

        // (a) Promoción: alumnos activos con inscripciones en el periodo que se cierra.
        if (currentActive is not null)
        {
            var closingOfferings = await offerings.GetByPeriodAsync(currentActive.Id, ct);
            var studentIds = closingOfferings.SelectMany(o => o.Enrollments.Select(e => e.StudentId)).Distinct();

            var studentsToPromote = new List<User>();
            foreach (var studentId in studentIds)
            {
                var student = await users.GetByIdAsync(studentId, ct);
                if (student is { Role: UserRole.Student, Status: UserStatus.Active })
                {
                    studentsToPromote.Add(student);
                }
            }

            var promotionResults = TermPromotionPolicy.PromoteAll(studentsToPromote, clock);
            if (promotionResults.Any(r => r.IsFailure))
            {
                return Result.Failure<string>(Error.Failure("OpenPeriod.PromotionFailed", "No se pudo promover a uno o más alumnos."));
            }

            foreach (var student in studentsToPromote)
            {
                await users.UpdateAsync(student, ct);
            }

            // (b) Cierre del periodo activo.
            var closeResult = currentActive.Close(currentUser.UserId ?? "system", clock);
            if (closeResult.IsFailure) return Result.Failure<string>(closeResult.Error);
            await periods.UpdateAsync(currentActive, ct);
        }

        // (c) Alta y activación del nuevo periodo.
        var newPeriod = AcademicPeriod.Open(EntityId.NewId(), codeResult.Value, command.Name, dateRangeResult.Value, currentUser.UserId ?? "system", clock);
        if (newPeriod.IsFailure) return Result.Failure<string>(newPeriod.Error);

        await periods.AddAsync(newPeriod.Value, ct);
        return Result.Success(newPeriod.Value.Id);
    }
}
