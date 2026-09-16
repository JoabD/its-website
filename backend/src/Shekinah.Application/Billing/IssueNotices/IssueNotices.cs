using Shekinah.Application.Abstractions;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.IssueNotices;

/// <summary>
/// RN-19: calcula meses adeudados del periodo activo, envía correo con la lista y el número de
/// aviso, e incrementa el contador. Sin <c>StudentId</c> ⇒ masivo (contrato §7). Solo se notifica
/// a quien efectivamente debe algún mes.
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator)]
public sealed record IssueNoticesCommand(string? StudentId) : ICommand<IssueNoticesResponse>;

public sealed record IssueNoticesResponse(int NoticesIssued, int UsersBlocked);

public sealed class IssueNoticesCommandHandler(
    IUserRepository users, IPaymentRepository payments, IPaymentNoticeRepository notices,
    Domain.Academics.IAcademicPeriodRepository periods, IEmailSender emailSender, IRegionScopeResolver regionScope, IClock clock)
    : ICommandHandler<IssueNoticesCommand, IssueNoticesResponse>
{
    public async Task<Result<IssueNoticesResponse>> HandleAsync(IssueNoticesCommand command, CancellationToken ct)
    {
        var activePeriod = await periods.GetActiveAsync(ct);
        if (activePeriod is null)
        {
            return Result.Failure<IssueNoticesResponse>(Error.Conflict("IssueNotices.NoActivePeriod", "No hay un periodo activo."));
        }

        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();

        List<User> targets;
        if (!string.IsNullOrWhiteSpace(command.StudentId))
        {
            var single = await users.GetByIdAsync(command.StudentId, ct);
            targets = single is not null ? [single] : [];
        }
        else
        {
            targets = (await users.GetActiveStudentsInRegionAsync(mandatoryRegionId ?? string.Empty, currentTerm: null, ct)).ToList();
        }

        var issued = 0;
        var blocked = 0;

        foreach (var student in targets)
        {
            if (mandatoryRegionId is not null && student.Region?.Id != mandatoryRegionId)
            {
                continue; // RN-09: un coordinador jamás notifica fuera de su región.
            }

            var paidMonths = await payments.GetPaidMonthsAsync(student.Id, ct);
            var monthsDue = DelinquencyPolicy.CalculateMonthsDue(activePeriod.MonthCodes, paidMonths);

            if (!DelinquencyPolicy.ShouldNotify(monthsDue))
            {
                continue;
            }

            var noticeNumber = await notices.GetNextNoticeNumberAsync(student.Id, ct);
            var noticeResult = PaymentNotice.Issue(
                EntityId.NewId(),
                new StudentRef(student.Id, student.EnrollmentNumber.Value, student.Profile.FullName.FullName),
                noticeNumber, monthsDue, activePeriod.Id, "system", clock);

            if (noticeResult.IsFailure)
            {
                continue;
            }

            await notices.AddAsync(noticeResult.Value, ct);

            var wasBlocked = student.Billing.IsBlocked;
            student.RegisterPaymentNotice(clock);
            await users.UpdateAsync(student, ct);

            if (!wasBlocked && student.Billing.IsBlocked)
            {
                blocked++;
            }

            await emailSender.SendAsync(
                student.Profile.Email.Value,
                $"Aviso de adeudo #{noticeNumber} — Instituto Teológico Shekinah",
                $"<p>Tienes pendientes los meses: {string.Join(", ", monthsDue.Select(m => m.Value))}.</p>",
                ct);

            issued++;
        }

        return Result.Success(new IssueNoticesResponse(issued, blocked));
    }
}
