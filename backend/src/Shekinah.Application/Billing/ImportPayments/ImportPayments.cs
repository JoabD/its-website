using Shekinah.Application.Abstractions;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.ImportPayments;

/// <summary>
/// RN-18: acepta .xlsx/.csv con columnas USUARIO y PERIODO; valida numéricas y PERIODO=^\d{6}$;
/// upsert idempotente por (alumno, mes); reinicia el contador de avisos y desbloquea; reporta
/// fila a fila sin abortar el lote.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record ImportPaymentsCommand(string FileName, Stream Content) : ICommand<ImportPaymentsResponse>;

public sealed record ImportPaymentsResponse(string BatchId, int TotalRows, int ImportedRows, IReadOnlyList<PaymentImportRowError> Errors);

public sealed class ImportPaymentsCommandHandler(
    ISpreadsheetReader spreadsheetReader, IPaymentRepository payments, IUserRepository users,
    Domain.Academics.IAcademicPeriodRepository periods, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ImportPaymentsCommand, ImportPaymentsResponse>
{
    public async Task<Result<ImportPaymentsResponse>> HandleAsync(ImportPaymentsCommand command, CancellationToken ct)
    {
        if (!spreadsheetReader.CanRead(command.FileName))
        {
            return Result.Failure<ImportPaymentsResponse>(Error.Validation("ImportPayments.UnsupportedFormat", "Formato de archivo no soportado (use .xlsx o .csv)."));
        }

        var rows = new List<SpreadsheetRow>();
        await foreach (var row in spreadsheetReader.ReadAsync(command.Content, command.FileName, ct))
        {
            rows.Add(row);
        }

        var batch = await payments.RegisterImportBatchAsync(command.FileName, currentUser.UserId ?? "system", rows.Count, ct);
        var errors = new List<PaymentImportRowError>();
        var imported = 0;

        var activePeriod = await periods.GetActiveAsync(ct);

        foreach (var row in rows)
        {
            var raw = new[] { row.Values.GetValueOrDefault("USUARIO", ""), row.Values.GetValueOrDefault("PERIODO", "") };

            if (!int.TryParse(row.Values.GetValueOrDefault("USUARIO"), out var enrollmentNumberRaw))
            {
                errors.Add(new PaymentImportRowError(row.RowNumber, "INVALID_USUARIO", "USUARIO debe ser numérico.", raw));
                continue;
            }

            var monthCodeResult = MonthCode.Create(row.Values.GetValueOrDefault("PERIODO"));
            if (monthCodeResult.IsFailure)
            {
                errors.Add(new PaymentImportRowError(row.RowNumber, "INVALID_PERIODO", "PERIODO debe cumplir el formato YYYYMM.", raw));
                continue;
            }

            var enrollmentNumberResult = EnrollmentNumber.Create(enrollmentNumberRaw);
            if (enrollmentNumberResult.IsFailure)
            {
                errors.Add(new PaymentImportRowError(row.RowNumber, "INVALID_USUARIO", "USUARIO no es una matrícula válida.", raw));
                continue;
            }

            var student = await users.GetByEnrollmentNumberAsync(enrollmentNumberResult.Value, ct);
            if (student is null)
            {
                errors.Add(new PaymentImportRowError(row.RowNumber, "STUDENT_NOT_FOUND", $"No existe un alumno con matrícula {enrollmentNumberRaw}.", raw));
                continue;
            }

            var existingPayment = await payments.FindAsync(student.Id, monthCodeResult.Value, ct);
            if (existingPayment is null)
            {
                var paymentResult = Payment.Register(
                    EntityId.NewId(),
                    new StudentRef(student.Id, student.EnrollmentNumber.Value, student.Profile.FullName.FullName),
                    monthCodeResult.Value, activePeriod?.Id ?? string.Empty, null, PaymentSource.Import, batch.Id,
                    currentUser.UserId ?? "system", clock);

                if (paymentResult.IsFailure)
                {
                    errors.Add(new PaymentImportRowError(row.RowNumber, "PAYMENT_INVALID", paymentResult.Error.Message, raw));
                    continue;
                }

                await payments.AddAsync(paymentResult.Value, ct);
            }
            // else: upsert idempotente — la fila ya existía, no se duplica (RN-18/RN-21).

            student.ResetDelinquency();
            await users.UpdateAsync(student, ct);
            imported++;
        }

        await payments.CompleteImportBatchAsync(batch.Id, imported, errors, ct);
        return Result.Success(new ImportPaymentsResponse(batch.Id, rows.Count, imported, errors));
    }
}
