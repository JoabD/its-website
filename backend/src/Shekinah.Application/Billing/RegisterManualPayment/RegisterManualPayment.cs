using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.RegisterManualPayment;

/// <summary>
/// Plan de control escolar, fase 3: mientras no exista pasarela de pagos en línea, una cuenta
/// maestra o de apoyo regional puede marcar manualmente un mes como pagado directamente desde la
/// matriz de pagos (antes solo se podía vía importación masiva de .xlsx/.csv). Reutiliza
/// <see cref="Payment"/> con <see cref="PaymentSource.Manual"/> — el día que haya pagos en línea,
/// ese flujo simplemente agregará <c>PaymentSource.Online</c> sin tocar nada de aquí.
/// RN-09: un coordinador/secretario regional solo puede marcar pagos de alumnos de su propia
/// región — se valida en el handler contra <see cref="IRegionScopeResolver"/>, nunca confiando en
/// el cliente.
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record RegisterManualPaymentCommand(string StudentId, string MonthCode) : ICommand<ManualPaymentResponse>;

public sealed record ManualPaymentResponse(string StudentId, string MonthCode, bool Paid);

public sealed class RegisterManualPaymentCommandValidator : AbstractValidator<RegisterManualPaymentCommand>
{
    public RegisterManualPaymentCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.MonthCode).NotEmpty();
    }
}

public sealed class RegisterManualPaymentCommandHandler(
    IUserRepository users, IPaymentRepository payments, Domain.Academics.IAcademicPeriodRepository periods,
    IRegionScopeResolver regionScope, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<RegisterManualPaymentCommand, ManualPaymentResponse>
{
    public async Task<Result<ManualPaymentResponse>> HandleAsync(RegisterManualPaymentCommand command, CancellationToken ct)
    {
        var monthCodeResult = MonthCode.Create(command.MonthCode);
        if (monthCodeResult.IsFailure)
        {
            return Result.Failure<ManualPaymentResponse>(monthCodeResult.Error);
        }

        var student = await users.GetByIdAsync(command.StudentId, ct);
        if (student is null || student.Role != UserRole.Student)
        {
            return Result.Failure<ManualPaymentResponse>(Error.NotFound("RegisterManualPayment.StudentNotFound", "El alumno indicado no existe."));
        }

        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        if (mandatoryRegionId is not null && student.Region?.Id != mandatoryRegionId)
        {
            return Result.Failure<ManualPaymentResponse>(Error.Forbidden("RegisterManualPayment.OutOfScope", "No puede registrar pagos de alumnos fuera de su región."));
        }

        var existing = await payments.FindAsync(student.Id, monthCodeResult.Value, ct);
        if (existing is not null)
        {
            return Result.Failure<ManualPaymentResponse>(Error.Conflict("RegisterManualPayment.AlreadyPaid", "Ese mes ya tiene un pago registrado."));
        }

        var activePeriod = await periods.GetActiveAsync(ct);

        var paymentResult = Payment.Register(
            EntityId.NewId(),
            new StudentRef(student.Id, student.EnrollmentNumber.Value, student.Profile.FullName.FullName),
            monthCodeResult.Value, activePeriod?.Id ?? string.Empty, null, PaymentSource.Manual,
            null, currentUser.UserId ?? "system", clock);

        if (paymentResult.IsFailure)
        {
            return Result.Failure<ManualPaymentResponse>(paymentResult.Error);
        }

        await payments.AddAsync(paymentResult.Value, ct);

        // RN-18: registrar un pago siempre reinicia el contador de avisos y desbloquea, igual que
        // la importación masiva — un pago manual no debe dejar al alumno bloqueado por morosidad.
        student.ResetDelinquency();
        await users.UpdateAsync(student, ct);

        return Result.Success(new ManualPaymentResponse(student.Id, monthCodeResult.Value.Value, true));
    }
}
