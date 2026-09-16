using Shekinah.Domain.Common;

namespace Shekinah.Domain.Identity;

/// <summary>
/// Contador de avisos de morosidad (USUARIOS.PAGOS legado) y bloqueo asociado (RN-07, RN-19).
/// </summary>
public sealed record BillingState
{
    public const int MaxNoticesBeforeBlock = 3;

    public int NoticeCount { get; }

    public DateTime? LastNoticeAtUtc { get; }

    public DateTime? BlockedAtUtc { get; }

    public bool IsBlocked => BlockedAtUtc is not null;

    private BillingState(int noticeCount, DateTime? lastNoticeAtUtc, DateTime? blockedAtUtc)
    {
        NoticeCount = noticeCount;
        LastNoticeAtUtc = lastNoticeAtUtc;
        BlockedAtUtc = blockedAtUtc;
    }

    public static BillingState Clean => new(0, null, null);

    public static BillingState Rehydrate(int noticeCount, DateTime? lastNoticeAtUtc, DateTime? blockedAtUtc) =>
        new(noticeCount, lastNoticeAtUtc, blockedAtUtc);

    /// <summary>RN-19: al superar 3 avisos (es decir, en el 4.º) el usuario queda bloqueado.</summary>
    public BillingState RegisterNotice(IClock clock)
    {
        var count = NoticeCount + 1;
        var blockedAt = count > MaxNoticesBeforeBlock ? clock.UtcNow : BlockedAtUtc;
        return new BillingState(count, clock.UtcNow, blockedAt);
    }

    /// <summary>RN-18: la importación de pagos reinicia el contador y desbloquea.</summary>
    public BillingState ResetAfterPayment() => new(0, LastNoticeAtUtc, null);
}
