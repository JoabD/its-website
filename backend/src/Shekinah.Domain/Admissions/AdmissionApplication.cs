using Shekinah.Domain.Admissions.Events;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Admissions;

public sealed record ApplicationDocument(string FileId, string FileName, string ContentType, long SizeBytes, DateTime UploadedAtUtc);

public sealed record ApplicationDecision(DateTime DecidedAtUtc, string DecidedByUserId, string DecidedByName, string? Reason);

/// <summary>
/// Agregado raíz del contexto Admissions. RN-01: cualquiera puede solicitar sin autenticarse;
/// una vez decidida (Approved/Rejected) es INMUTABLE — no expone ningún método que permita mutar
/// <see cref="Applicant"/> o <see cref="Modality"/> tras la decisión.
/// </summary>
public sealed class AdmissionApplication : AggregateRoot<string>
{
    private readonly List<ApplicationDocument> _documents = [];

    private AdmissionApplication() { }

    private AdmissionApplication(string id, string folio, int? legacyId, ApplicantProfile applicant, ModalityChoice modality, IClock clock)
        : base(id)
    {
        Folio = folio;
        LegacyId = legacyId;
        Applicant = applicant;
        ModalityChoice = modality;
        Status = ApplicationStatus.Pending;
        SubmittedAtUtc = clock.UtcNow;
    }

    public string Folio { get; private set; } = string.Empty;

    public int? LegacyId { get; private set; }

    public ApplicantProfile Applicant { get; private set; } = null!;

    public ModalityChoice ModalityChoice { get; private set; } = null!;

    public IReadOnlyList<ApplicationDocument> Documents => _documents.AsReadOnly();

    public ApplicationStatus Status { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public ApplicationDecision? Decision { get; private set; }

    public string? CreatedUserId { get; private set; }

    /// <summary>RN-01: alta de una nueva solicitud, siempre en estado Pending.</summary>
    public static Result<AdmissionApplication> Submit(
        string id, string folio, ApplicantProfile applicant, ModalityChoice modality,
        IEnumerable<ApplicationDocument> documents, IClock clock)
    {
        var application = new AdmissionApplication(id, folio, null, applicant, modality, clock);
        application._documents.AddRange(documents);
        application.Raise(new AdmissionApplicationSubmitted(Guid.NewGuid(), clock.UtcNow, id, folio));
        return Result.Success(application);
    }

    /// <summary>Reconstrucción desde el migrador (Fase 8), conservando el legacyId y el estado ya decidido.</summary>
    public static AdmissionApplication Rehydrate(
        string id, string folio, int legacyId, ApplicantProfile applicant, ModalityChoice modality,
        ApplicationStatus status, DateTime submittedAtUtc, ApplicationDecision? decision, string? createdUserId)
    {
        var application = new AdmissionApplication
        {
            Id = id,
            Folio = folio,
            LegacyId = legacyId,
            Applicant = applicant,
            ModalityChoice = modality,
            Status = status,
            SubmittedAtUtc = submittedAtUtc,
            Decision = decision,
            CreatedUserId = createdUserId,
        };
        return application;
    }

    /// <summary>RN-05 (mitad de dominio: la creación del User ocurre en Application/Infrastructure dentro de la misma transacción).</summary>
    public Result Approve(string decidedByUserId, string decidedByName, string createdUserId, IClock clock)
    {
        if (Status != ApplicationStatus.Pending)
        {
            return Result.Failure(Error.Conflict("AdmissionApplication.NotPending", "La solicitud ya fue decidida y es inmutable."));
        }

        Status = ApplicationStatus.Approved;
        Decision = new ApplicationDecision(clock.UtcNow, decidedByUserId, decidedByName, null);
        CreatedUserId = createdUserId;
        Raise(new AdmissionApplicationApproved(Guid.NewGuid(), clock.UtcNow, Id, decidedByUserId, decidedByName));
        return Result.Success();
    }

    /// <summary>RN-06.</summary>
    public Result Reject(string decidedByUserId, string decidedByName, string reason, IClock clock)
    {
        if (Status != ApplicationStatus.Pending)
        {
            return Result.Failure(Error.Conflict("AdmissionApplication.NotPending", "La solicitud ya fue decidida y es inmutable."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("AdmissionApplication.ReasonRequired", "El motivo de rechazo es requerido."));
        }

        Status = ApplicationStatus.Rejected;
        Decision = new ApplicationDecision(clock.UtcNow, decidedByUserId, decidedByName, reason.Trim());
        Raise(new AdmissionApplicationRejected(Guid.NewGuid(), clock.UtcNow, Id, decidedByUserId, reason.Trim()));
        return Result.Success();
    }
}
