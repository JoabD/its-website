using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

public sealed record DateRange
{
    public DateTime StartsOnUtc { get; }

    public DateTime EndsOnUtc { get; }

    private DateRange(DateTime startsOnUtc, DateTime endsOnUtc)
    {
        StartsOnUtc = startsOnUtc;
        EndsOnUtc = endsOnUtc;
    }

    public static Result<DateRange> Create(DateTime startsOnUtc, DateTime endsOnUtc)
    {
        if (endsOnUtc < startsOnUtc)
        {
            return Result.Failure<DateRange>(Error.Validation("DateRange.Invalid", "La fecha final debe ser mayor o igual a la fecha inicial."));
        }

        return Result.Success(new DateRange(startsOnUtc, endsOnUtc));
    }

    public bool Contains(DateTime moment) => moment >= StartsOnUtc && moment <= EndsOnUtc;
}
