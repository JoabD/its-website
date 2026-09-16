using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.SubmitApplication;

/// <summary>RN-01: cualquier persona puede enviar una solicitud sin autenticarse.</summary>
[AllowAnonymousUseCase]
public sealed record SubmitApplicationCommand(
    string FullName, DateOnly BirthDate, string MaritalStatus, string Email, string Phone,
    string Street, string Neighborhood, string Locality, string Municipality, string State,
    string ChurchName, string ChurchStreet, string ChurchNeighborhood, string ChurchLocality, string ChurchMunicipality,
    string PastorName, string TimeAttending, bool HasMinistryRole, string? MinistryRoleName,
    SchoolingLevel EducationLevel, string? OtherEducationDescription, string TheologicalBackground, string StudyPurpose,
    Modality Modality, string? RequestedRegionId, string? OnlineReason) : ICommand<SubmitApplicationResponse>;

public sealed record SubmitApplicationResponse(string ApplicationId, string Folio);

public sealed class SubmitApplicationCommandValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.StudyPurpose).NotEmpty();
        RuleFor(x => x.OnlineReason).NotEmpty().When(x => x.Modality == Modality.Online)
            .WithMessage("El motivo es requerido para la modalidad Virtual (RN-03).");
        RuleFor(x => x.RequestedRegionId).NotEmpty().When(x => x.Modality == Modality.Onsite)
            .WithMessage("Debe elegir una región presencial (RN-03).");
    }
}

/// <summary>RN-01, RN-02, RN-03, RN-04.</summary>
public sealed class SubmitApplicationCommandHandler(
    IAdmissionApplicationRepository applications, Domain.Catalog.IRegionRepository regions,
    IUserRepository administratorsSource, IEmailSender emailSender, INotificationRecipients notificationRecipients, IClock clock)
    : ICommandHandler<SubmitApplicationCommand, SubmitApplicationResponse>
{
    public async Task<Result<SubmitApplicationResponse>> HandleAsync(SubmitApplicationCommand command, CancellationToken ct)
    {
        var address = Address.Create(command.Street, command.Neighborhood, command.Locality, command.Municipality, command.State);
        if (address.IsFailure) return Result.Failure<SubmitApplicationResponse>(address.Error);

        var churchAddress = Address.Create(command.ChurchStreet, command.ChurchNeighborhood, command.ChurchLocality, command.ChurchMunicipality);
        if (churchAddress.IsFailure) return Result.Failure<SubmitApplicationResponse>(churchAddress.Error);

        var ministryRole = MinistryRole.Create(command.HasMinistryRole, command.MinistryRoleName);
        if (ministryRole.IsFailure) return Result.Failure<SubmitApplicationResponse>(ministryRole.Error);

        var church = ChurchInfo.Create(command.ChurchName, churchAddress.Value, command.PastorName, command.TimeAttending, ministryRole.Value);
        if (church.IsFailure) return Result.Failure<SubmitApplicationResponse>(church.Error);

        var education = EducationLevel.Create(command.EducationLevel, command.OtherEducationDescription);
        if (education.IsFailure) return Result.Failure<SubmitApplicationResponse>(education.Error);

        var name = PersonName.Create(command.FullName);
        var email = Email.Create(command.Email);
        var phone = PhoneNumber.Create(command.Phone);
        if (name.IsFailure) return Result.Failure<SubmitApplicationResponse>(name.Error);
        if (email.IsFailure) return Result.Failure<SubmitApplicationResponse>(email.Error);
        if (phone.IsFailure) return Result.Failure<SubmitApplicationResponse>(phone.Error);

        var applicant = ApplicantProfile.Create(
            name.Value, command.BirthDate, command.MaritalStatus, email.Value, phone.Value, address.Value, church.Value,
            education.Value, command.TheologicalBackground, command.StudyPurpose);
        if (applicant.IsFailure) return Result.Failure<SubmitApplicationResponse>(applicant.Error);

        // RN-03: la región se DERIVA de la modalidad, resuelta por el domain service, no por el cliente.
        var activeRegions = await regions.GetActiveAsync(ct);
        var regionResult = Domain.Catalog.RegionAssignmentPolicy.Resolve(command.Modality, activeRegions, command.RequestedRegionId);
        if (regionResult.IsFailure) return Result.Failure<SubmitApplicationResponse>(regionResult.Error);

        var modalityChoice = ModalityChoice.Create(command.Modality, regionResult.Value, command.OnlineReason);
        if (modalityChoice.IsFailure) return Result.Failure<SubmitApplicationResponse>(modalityChoice.Error);

        var sequence = await applications.GetNextLegacySequenceAsync(ct);
        var folio = $"SOL-{clock.UtcNow:yyyy}-{sequence:D6}";

        var applicationResult = AdmissionApplication.Submit(
            EntityId.NewId(), folio, applicant.Value, modalityChoice.Value, [], clock);
        if (applicationResult.IsFailure) return Result.Failure<SubmitApplicationResponse>(applicationResult.Error);

        await applications.AddAsync(applicationResult.Value, ct);

        // RN-04: notifica a todos los administradores activos; el fallo del correo NO revierte la solicitud (outbox).
        // Correo administrativo ⇒ se copia (Cc) la dirección de EmailSettings.DefaultCcAddress si está configurada.
        var administrators = await administratorsSource.GetActiveAdministratorsAsync(ct);
        foreach (var admin in administrators)
        {
            await emailSender.SendAsync(
                admin.Profile.Email.Value,
                $"Nueva solicitud de admisión: {folio}",
                $"<p>Se recibió una nueva solicitud de {applicant.Value.FullName} ({folio}).</p>", ct,
                cc: notificationRecipients.AdministrativeCc);
        }

        return Result.Success(new SubmitApplicationResponse(applicationResult.Value.Id, folio));
    }
}
