using System.Text.RegularExpressions;
using Shekinah.Domain.Common;

namespace Shekinah.Domain.SharedKernel;

/// <summary>
/// Mes de cobro en formato "YYYYMM", ordenable lexicográficamente (spec técnico §5.1). Es la
/// unidad atómica de RN-17, RN-18, RN-19, RN-21.
/// </summary>
public sealed partial record MonthCode : IComparable<MonthCode>
{
    public string Value { get; }

    private MonthCode(string value) => Value = value;

    public static Result<MonthCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Pattern().IsMatch(value))
        {
            return Result.Failure<MonthCode>(Error.Validation("MonthCode.Invalid", "El mes debe tener el formato YYYYMM."));
        }

        var month = int.Parse(value[4..]);
        if (month is < 1 or > 12)
        {
            return Result.Failure<MonthCode>(Error.Validation("MonthCode.InvalidMonth", "El mes (MM) debe estar entre 01 y 12."));
        }

        return Result.Success(new MonthCode(value));
    }

    public static MonthCode FromYearMonth(int year, int month) => new(FormattableString.Invariant($"{year:D4}{month:D2}"));

    /// <summary>Genera la serie inclusiva desde este mes hasta <paramref name="to"/> (RN-17).</summary>
    public IReadOnlyList<MonthCode> UpTo(MonthCode to)
    {
        var start = new DateTime(int.Parse(Value[..4]), int.Parse(Value[4..]), 1);
        var end = new DateTime(int.Parse(to.Value[..4]), int.Parse(to.Value[4..]), 1);

        var result = new List<MonthCode>();
        for (var cursor = start; cursor <= end; cursor = cursor.AddMonths(1))
        {
            result.Add(FromYearMonth(cursor.Year, cursor.Month));
        }

        return result;
    }

    public int CompareTo(MonthCode? other) => other is null ? 1 : string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex Pattern();
}
