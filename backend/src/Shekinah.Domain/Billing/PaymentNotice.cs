using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Billing;

/// <summary>Agregado Billing. Solo se emite si hay al menos un mes adeudado; noticeNumber ≥ 1 (spec técnico §2.2).</summary>
public sealed class PaymentNotice : AggregateRoot<string>
{
    private readonly List<MonthCode> _monthsDue = [];

    private PaymentNotice() { }

    private PaymentNotice(string id, StudentRef student, int noticeNumber, IReadOnlyList<MonthCode> monthsDue, string periodId, string issuedByUserId, IClock clock)
        : base(id)
    {
        Student = student;
        NoticeNumber = noticeNumber;
        _monthsDue.AddRange(monthsDue);
        PeriodId = periodId;
        Channel = NoticeChannel.Email;
        IssuedAtUtc = clock.UtcNow;
        IssuedByUserId = issuedByUserId;
        ResultedInBlock = noticeNumber > Identity.BillingState.MaxNoticesBeforeBlock;
    }

    public StudentRef Student { get; private set; } = null!;

    public int NoticeNumber { get; private set; }

    public IReadOnlyList<MonthCode> MonthsDue => _monthsDue.AsReadOnly();

    public string PeriodId { get; private set; } = string.Empty;

    public NoticeChannel Channel { get; private set; }

    public DateTime IssuedAtUtc { get; private set; }

    public string IssuedByUserId { get; private set; } = string.Empty;

    public bool ResultedInBlock { get; private set; }

    /// <summary>RN-19: solo se notifica a quien efectivamente debe algún mes.</summary>
    public static Result<PaymentNotice> Issue(string id, StudentRef student, int noticeNumber, IReadOnlyList<MonthCode> monthsDue, string periodId, string issuedByUserId, IClock clock)
    {
        if (monthsDue.Count == 0)
        {
            return Result.Failure<PaymentNotice>(Error.Validation("PaymentNotice.NoDebt", "No se puede emitir un aviso a un alumno sin meses adeudados."));
        }

        if (noticeNumber < 1)
        {
            return Result.Failure<PaymentNotice>(Error.Validation("PaymentNotice.InvalidNumber", "El número de aviso debe ser ≥ 1."));
        }

        return Result.Success(new PaymentNotice(id, student, noticeNumber, monthsDue, periodId, issuedByUserId, clock));
    }
}
