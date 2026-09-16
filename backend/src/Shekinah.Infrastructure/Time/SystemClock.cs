using Shekinah.Domain.Common;

namespace Shekinah.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
