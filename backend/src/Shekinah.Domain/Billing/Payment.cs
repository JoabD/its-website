using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Billing;

public sealed record StudentRef(string Id, int EnrollmentNumber, string Name);

public enum PaymentSource
{
    Import,
    Manual,
    Legacy,
}

/// <summary>Agregado Billing. Único por (alumno, mes) — RN-21. No embebido en User: crece sin cota (spec técnico §5.1).</summary>
public sealed class Payment : AggregateRoot<string>
{
    private Payment() { }

    private Payment(string id, StudentRef student, MonthCode monthCode, string periodId, Money? amount, PaymentSource source, string? importBatchId, string registeredByUserId, IClock clock)
        : base(id)
    {
        Student = student;
        MonthCode = monthCode;
        PeriodId = periodId;
        Amount = amount;
        Source = source;
        ImportBatchId = importBatchId;
        RegisteredAtUtc = clock.UtcNow;
        RegisteredByUserId = registeredByUserId;
    }

    public StudentRef Student { get; private set; } = null!;

    public MonthCode MonthCode { get; private set; } = null!;

    public string PeriodId { get; private set; } = string.Empty;

    /// <summary>Opcional por diseño: el legado no guarda monto ni fecha (PROMPT-MAESTRO.md §11, ADR "Datos de cobranza").</summary>
    public Money? Amount { get; private set; }

    public PaymentSource Source { get; private set; }

    public string? ImportBatchId { get; private set; }

    public DateTime RegisteredAtUtc { get; private set; }

    public string RegisteredByUserId { get; private set; } = string.Empty;

    public static Result<Payment> Register(string id, StudentRef student, MonthCode monthCode, string periodId, Money? amount, PaymentSource source, string? importBatchId, string registeredByUserId, IClock clock) =>
        Result.Success(new Payment(id, student, monthCode, periodId, amount, source, importBatchId, registeredByUserId, clock));

    public static Payment Rehydrate(string id, StudentRef student, MonthCode monthCode, string periodId, Money? amount, PaymentSource source, string? importBatchId, DateTime registeredAtUtc, string registeredByUserId) => new()
    {
        Id = id,
        Student = student,
        MonthCode = monthCode,
        PeriodId = periodId,
        Amount = amount,
        Source = source,
        ImportBatchId = importBatchId,
        RegisteredAtUtc = registeredAtUtc,
        RegisteredByUserId = registeredByUserId,
    };
}
