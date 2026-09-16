using Shekinah.Domain.Common;

namespace Shekinah.Domain.UnitTests;

public sealed class TestClock(DateTime? fixedUtcNow = null) : IClock
{
    public DateTime UtcNow { get; set; } = fixedUtcNow ?? new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
}
