using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.IssueNotices;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.RunScheduledNotices;

/// <summary>
/// Panel de verificación de pagos (docs/Plan-Panel-Pagos.md, sección 7 — "cron" semanal de avisos
/// de mora): mismo trabajo que <c>IssueNoticesCommand(StudentId: null)</c>, pero pensado para
/// dispararse SOLO por el job programado (GitHub Actions, `schedule` semanal), nunca por un usuario
/// logueado — por eso es <see cref="AllowAnonymousUseCaseAttribute"/> en vez de
/// <see cref="RequireRoleAttribute"/>: no hay una sesión de Administrator detrás de un cron. La
/// protección real no vive aquí sino en el endpoint (PaymentsEndpoints: header secreto compartido
/// contra Jobs:ScheduledNoticesSecret) — este comando por sí solo confía en quien ya pasó esa
/// puerta.
///
/// Se duplica (en vez de reusar) el cuerpo de <see cref="IssueNoticesCommandHandler"/> a propósito:
/// ese handler depende de <see cref="ICurrentUser"/>/<see cref="IRegionScopeResolver"/> para acotar
/// por región cuando quien pide es un RegionalCoordinator — un concepto que no aplica a un job de
/// sistema, que siempre debe cubrir TODAS las regiones. Forzar ese caso por el mismo handler habría
/// significado enredar la regla de alcance regional con "no hay usuario" — más riesgoso que estas
/// ~30 líneas repetidas.
/// </summary>
[AllowAnonymousUseCase]
public sealed record RunScheduledNoticesCommand : ICommand<IssueNoticesResponse>;

public sealed class RunScheduledNoticesCommandHandler(
    Domain.Identity.IUserRepository users, IPaymentRepository payments, IPaymentNoticeRepository notices,
    Domain.Academics.IAcademicPeriodRepository periods, IEmailSender emailSender, IClock clock)
    : ICommandHandler<RunScheduledNoticesCommand, IssueNoticesResponse>
{
    public async Task<Result<IssueNoticesResponse>> HandleAsync(RunScheduledNoticesCommand command, CancellationToken ct)
    {
        var activePeriod = await periods.GetActiveAsync(ct);
        if (activePeriod is null)
        {
            return Result.Success(new IssueNoticesResponse(0, 0));
        }

        // Sin regionId => todas las regiones (ver UserRepository.GetActiveStudentsInRegionAsync:
        // un regionId vacío no filtra) — a propósito, un job de sistema siempre cubre todo.
        var targets = await users.GetActiveStudentsInRegionAsync(string.Empty, currentTerm: null, ct);

        var issued = 0;
        var blocked = 0;

        foreach (var student in targets)
        {
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
                noticeNumber, monthsDue, activePeriod.Id, "system-cron", clock);

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

            // Sin correo, el aviso igual se registra y cuenta para el bloqueo (RN-19/RN-07) — solo
            // se omite la notificación por correo, que no tiene a quién llegar.
            if (student.Profile.Email is not null)
            {
                await emailSender.SendAsync(
                    student.Profile.Email.Value,
                    $"Aviso de adeudo #{noticeNumber} — Instituto Teológico Shekinah",
                    $"<p>Tienes pendientes los meses: {string.Join(", ", monthsDue.Select(m => m.Value))}.</p><p style=\"color:#8994a8;font-size:12px;\">Aviso automático semanal — Instituto Teológico Shekinah.</p>",
                    ct);
            }

            issued++;
        }

        return Result.Success(new IssueNoticesResponse(issued, blocked));
    }
}
