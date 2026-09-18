using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Admissions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Admissions.SubmitApplication;

/// <summary>Nombre del "action" de reCAPTCHA v3 que el frontend debe declarar al ejecutar
/// <c>grecaptcha.execute(siteKey, &#123; action &#125;)</c> — el backend rechaza cualquier token que
/// declare un action distinto (evita reusar un token obtenido para otra acción del sitio).</summary>
public static class RecaptchaActions
{
    public const string SubmitApplication = "submit_application";
}

/// <summary>RN-01: cualquier persona puede enviar una solicitud sin autenticarse — por eso lleva
/// reCAPTCHA v3 (<see cref="RecaptchaToken"/>): es el endpoint anónimo más expuesto a spam/bots.</summary>
[AllowAnonymousUseCase]
public sealed record SubmitApplicationCommand(
    string FullName, DateOnly BirthDate, string MaritalStatus, string Email, string Phone,
    string Street, string Neighborhood, string Locality, string Municipality, string State,
    string ChurchName, string ChurchStreet, string ChurchNeighborhood, string ChurchLocality, string ChurchMunicipality,
    string PastorName, string TimeAttending, bool HasMinistryRole, string? MinistryRoleName,
    SchoolingLevel EducationLevel, string? OtherEducationDescription, string TheologicalBackground, string StudyPurpose,
    Modality Modality, string? RequestedRegionId, string? OnlineReason, string RecaptchaToken) : ICommand<SubmitApplicationResponse>;

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
        RuleFor(x => x.RecaptchaToken).NotEmpty()
            .WithMessage("Falta el token de reCAPTCHA.");
    }
}

/// <summary>RN-01, RN-02, RN-03, RN-04.</summary>
public sealed class SubmitApplicationCommandHandler(
    IAdmissionApplicationRepository applications, Domain.Catalog.IRegionRepository regions,
    IUserRepository administratorsSource, IEmailSender emailSender, INotificationRecipients notificationRecipients,
    IAdmissionFichaPdfGenerator fichaPdfGenerator, IRecaptchaVerifier recaptchaVerifier, IClock clock)
    : ICommandHandler<SubmitApplicationCommand, SubmitApplicationResponse>
{
    public async Task<Result<SubmitApplicationResponse>> HandleAsync(SubmitApplicationCommand command, CancellationToken ct)
    {
        // Antiabuso primero (RN-01): ni siquiera se valida/crea nada si reCAPTCHA no aprueba el
        // token — así un bot no puede usar este endpoint anónimo para spamear correos ni llenar la
        // base de solicitudes falsas.
        var isHuman = await recaptchaVerifier.VerifyAsync(command.RecaptchaToken, RecaptchaActions.SubmitApplication, ct);
        if (!isHuman)
        {
            return Result.Failure<SubmitApplicationResponse>(
                Error.Validation("Recaptcha.Failed", "No pudimos verificar tu solicitud. Recarga la página e intenta de nuevo."));
        }

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
        var ficha = AdmissionFichaHtml.Render(folio, applicant.Value, modalityChoice.Value, clock.UtcNow);

        // La ficha también se adjunta como PDF con membrete institucional (logo del Shekinah) —
        // mismo documento para administración y para el solicitante, un único punto de generación.
        var fichaPdfBytes = fichaPdfGenerator.Generate(BuildFichaPdfModel(folio, applicant.Value, modalityChoice.Value, clock.UtcNow));
        var fichaAttachment = new EmailAttachment($"Ficha-{folio}.pdf", "application/pdf", fichaPdfBytes);
        var fichaAttachments = new[] { fichaAttachment };

        var administrators = await administratorsSource.GetActiveAdministratorsAsync(ct);
        foreach (var admin in administrators)
        {
            await emailSender.SendAsync(
                admin.Profile.Email.Value,
                $"Nueva solicitud de admisión: {folio}",
                $"<p>Se recibió una nueva solicitud de {applicant.Value.FullName} ({folio}).</p>{ficha}", ct,
                cc: notificationRecipients.AdministrativeCc,
                attachments: fichaAttachments);
        }

        // Confirmación al propio solicitante (plan de control escolar, fase 4): NUNCA lleva Cc
        // administrativo (IEmailSender: los correos personales no se copian a otra bandeja).
        await emailSender.SendAsync(
            applicant.Value.Email.Value,
            $"Hemos recibido tu solicitud — folio {folio}",
            $"""
             <p>Hola {applicant.Value.FullName},</p>
             <p>Confirmamos la recepción de tu solicitud de admisión al Instituto Teológico Shekinah, con folio <strong>{folio}</strong>.</p>
             <p><strong>Debes esperar instrucciones por este medio, o acudir a tu sede con esta ficha en mano para continuar tu proceso.</strong></p>
             {ficha}
             <p>Adjuntamos tu ficha de inscripción en PDF.</p>
             <p>Si tienes dudas, puedes responder a este correo o acudir directamente a tu sede.</p>
             """,
            ct,
            attachments: fichaAttachments);

        return Result.Success(new SubmitApplicationResponse(applicationResult.Value.Id, folio));
    }

    /// <summary>Mismas filas que <see cref="AdmissionFichaHtml.Render"/> (una sola fuente de verdad
    /// para el contenido de la ficha), en la forma plana que espera <see cref="IAdmissionFichaPdfGenerator"/>.</summary>
    private static AdmissionFichaPdfModel BuildFichaPdfModel(string folio, ApplicantProfile applicant, ModalityChoice modality, DateTime generatedAtUtc)
    {
        var modalityLabel = modality.Modality switch
        {
            Modality.Onsite => "Presencial",
            Modality.Online => "Virtual",
            Modality.Diploma => "Diplomado",
            _ => modality.Modality.ToString(),
        };

        var ministryRole = applicant.Church.MinistryRole.HasRole
            ? applicant.Church.MinistryRole.RoleName ?? "Sí"
            : "No";

        var rows = new List<AdmissionFichaPdfRow>
        {
            new("Nombre completo", applicant.FullName.FullName),
            new("Fecha de nacimiento", applicant.BirthDate.ToString("dd/MM/yyyy")),
            new("Estado civil", applicant.MaritalStatus),
            new("Correo", applicant.Email.Value),
            new("Teléfono", applicant.Phone.Value),
            new("Dirección", $"{applicant.Address.Street}, {applicant.Address.Neighborhood}, {applicant.Address.Locality}, {applicant.Address.Municipality}"),
            new("Modalidad", modalityLabel),
            new("Región / Sede", modality.RegionName),
            new("Iglesia", applicant.Church.Name),
            new("Pastor", applicant.Church.PastorName),
            new("Tiempo asistiendo", applicant.Church.TimeAttending),
            new("Rol ministerial", ministryRole),
            new("Nivel de estudios", applicant.Education.Level.ToString()),
            new("Propósito de estudio", applicant.StudyPurpose),
        };

        return new AdmissionFichaPdfModel(folio, applicant.FullName.FullName, applicant.Email.Value, generatedAtUtc, rows);
    }
}
