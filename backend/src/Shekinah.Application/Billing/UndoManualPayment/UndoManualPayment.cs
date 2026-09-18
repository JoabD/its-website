using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.RegisterManualPayment;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.UndoManualPayment;

/// <summary>
/// Reversa de un pago marcado manualmente (plan de control escolar, fase 3). Por integridad,
/// SOLO se permite revertir pagos con <see cref="PaymentSource.Manual"/> — nunca uno que vino de
/// importación masiva, del legado o (a futuro) de una pasarela en línea, para no perder rastro de
/// dinero real ya procesado por esos canales.
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record UndoManualPaymentCommand(string StudentId, string MonthCode) : ICommand<ManualPaymentResponse>;

public sealed class UndoManualPaymentCommandValidator : AbstractValidator<UndoManualPaymentCommand>
{
    public UndoManualPaymentCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.MonthCode).NotEmpty();
    }
}

public sealed class UndoManualPaymentCommandHandler(
    IUserRepository users, IPaymentRepository payments, IRegionScopeResolver regionScope)
    : ICommandHandler<UndoManualPaymentCommand, ManualPaymentResponse>
{
    public async Task<Result<ManualPaymentResponse>> HandleAsync(UndoManualPaymentCommand command, CancellationToken ct)
    {
        var monthCodeResult = MonthCode.Create(command.MonthCode);
        if (monthCodeResult.IsFailure)
        {
            return Result.Failure<ManualPaymentResponse>(monthCodeResult.Error);
        }

        var student = await users.GetByIdAsync(command.StudentId, ct);
        if (student is null)
        {
            return Result.Failure<ManualPaymentResponse>(Error.NotFound("UndoManualPayment.StudentNotFound", "El alumno indicado no existe."));
        }

        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        if (mandatoryRegionId is not null && student.Region?.Id != mandatoryRegionId)
        {
            return Result.Failure<ManualPaymentResponse>(Error.Forbidden("UndoManualPayment.OutOfScope", "No puede modificar pagos de alumnos fuera de su región."));
        }

        var payment = await payments.FindAsync(student.Id, monthCodeResult.Value, ct);
        if (payment is null)
        {
            return Result.Failure<ManualPaymentResponse>(Error.NotFound("UndoManualPayment.NotFound", "No existe un pago registrado para ese mes."));
        }

        if (payment.Source != PaymentSource.Manual)
        {
            return Result.Failure<ManualPaymentResponse>(Error.Validation("UndoManualPayment.NotManual", "Solo se pueden revertir pagos registrados manualmente."));
        }

        await payments.DeleteAsync(payment.Id, ct);

        return Result.Success(new ManualPaymentResponse(student.Id, monthCodeResult.Value.Value, false));
    }
}
