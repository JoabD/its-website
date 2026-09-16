using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.RejectApplication;

[RequireRole(UserRole.Administrator)]
public sealed record RejectApplicationCommand(string ApplicationId, string Reason) : ICommand<Abstractions.Unit>;

public sealed class RejectApplicationCommandValidator : AbstractValidator<RejectApplicationCommand>
{
    public RejectApplicationCommandValidator() => RuleFor(x => x.Reason).NotEmpty();
}

/// <summary>RN-06: no crea usuario; encola correo de rechazo.</summary>
public sealed class RejectApplicationCommandHandler(
    Domain.Admissions.IAdmissionApplicationRepository applications, IEmailSender emailSender, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<RejectApplicationCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(RejectApplicationCommand command, CancellationToken ct)
    {
        var application = await applications.GetByIdAsync(command.ApplicationId, ct);
        if (application is null)
        {
            return Result.Failure<Abstractions.Unit>(Error.NotFound("AdmissionApplication.NotFound", "Solicitud no encontrada."));
        }

        var result = application.Reject(currentUser.UserId ?? "system", "Administrador", command.Reason, clock);
        if (result.IsFailure)
        {
            return Result.Failure<Abstractions.Unit>(result.Error);
        }

        await applications.UpdateAsync(application, ct);

        await emailSender.SendAsync(
            application.Applicant.Email.Value,
            "Resultado de tu solicitud — Instituto Teológico Shekinah",
            $"<p>Lamentamos informarte que tu solicitud {application.Folio} no fue aprobada.</p><p>Motivo: {command.Reason}</p>",
            ct);

        return Result.Success(Abstractions.Unit.Value);
    }
}
