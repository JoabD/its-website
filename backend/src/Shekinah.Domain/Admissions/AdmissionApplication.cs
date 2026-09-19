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
///
/// Checklist de documentación (panel de revisión del admin): el wizard público no sube archivos hoy
/// (<see cref="Documents"/> siempre llega vacío desde ahí), así que el checklist es una confirmación
/// manual del administrador de que YA revisó cada documento físico/en papel entregado. Se guarda como
/// una lista de ids marcados contra el catálogo vivo de <see cref="ChecklistItemDefinition"/> — "completo"
/// se calcula siempre contra los ítems ACTIVOS del catálogo al momento de consultar (spec confirmada:
/// catálogo vivo, no una copia congelada al enviar la solicitud), así que agregar/quitar un documento
/// requerido desde Configuración aplica de inmediato a cualquier solicitud aún pendiente.
/// </summary>
public sealed class AdmissionApplication : AggregateRoot<string>
{
    private readonly List<ApplicationDocument> _documents = [];
    private readonly HashSet<string> _checkedChecklistItemIds = [];

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

    public IReadOnlyCollection<string> CheckedChecklistItemIds => _checkedChecklistItemIds;

    /// <summary>Vía por la que se aprobó — solo trazabilidad interna, nunca cambia el resultado de
    /// la aprobación (RN explícita: el botón rápido de la lista debe poder omitir el checklist).</summary>
    public bool ApprovedViaQuickAction { get; private set; }

    /// <summary>
    /// Fecha en la que el job de purga (<c>AdmissionApplicationPurgeJob</c>) puede eliminar este
    /// documento — 30 días después de decidida (Approved o Rejected, spec confirmada: consistente
    /// para ambas). Null mientras sigue Pending — esas solo se eliminan a mano (ver IsDeletable).
    /// </summary>
    public DateTime? PurgeScheduledAtUtc { get; private set; }

    /// <summary>"Eliminar" solo procede sobre solicitudes aún no decididas — una vez aprobada o
    /// rechazada, la decisión (y su vínculo con el User creado, si aplica) debe quedar trazable
    /// hasta que la purga automática la alcance.</summary>
    public bool IsDeletable => Status == ApplicationStatus.Pending;

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
        ApplicationStatus status, DateTime submittedAtUtc, ApplicationDecision? decision, string? createdUserId,
        IEnumerable<string>? checkedChecklistItemIds = null, bool approvedViaQuickAction = false,
        DateTime? purgeScheduledAtUtc = null)
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
            ApprovedViaQuickAction = approvedViaQuickAction,
            PurgeScheduledAtUtc = purgeScheduledAtUtc,
        };
        if (checkedChecklistItemIds is not null)
        {
            application._checkedChecklistItemIds.UnionWith(checkedChecklistItemIds);
        }
        return application;
    }

    /// <summary>
    /// "Completo" = todos los <paramref name="activeItemIds"/> (catálogo vivo, resuelto por quien
    /// llama — el dominio no conoce el repositorio de catálogo) están marcados en esta solicitud.
    /// Catálogo vacío (cero documentos activos configurados) se considera trivialmente completo.
    /// </summary>
    public bool IsChecklistCompleteFor(IReadOnlyCollection<string> activeItemIds) =>
        activeItemIds.All(id => _checkedChecklistItemIds.Contains(id));

    /// <summary>
    /// Reemplaza el conjunto de ítems marcados — solo mientras la solicitud siga Pending (una vez
    /// decidida, todo el agregado es inmutable, checklist incluido).
    /// </summary>
    public Result UpdateChecklist(IEnumerable<string> checkedItemIds)
    {
        if (Status != ApplicationStatus.Pending)
        {
            return Result.Failure(Error.Conflict("AdmissionApplication.NotPending", "La solicitud ya fue decidida y es inmutable."));
        }

        _checkedChecklistItemIds.Clear();
        _checkedChecklistItemIds.UnionWith(checkedItemIds);
        return Result.Success();
    }

    /// <summary>
    /// RN-05 (mitad de dominio: la creación del User ocurre en Application/Infrastructure dentro de
    /// la misma transacción). <paramref name="viaQuickAction"/>: true cuando se aprueba directo
    /// desde la lista (se infiere que el administrador ya tiene toda la documentación en mano, por
    /// eso NO se exige el checklist completo en ese camino) — solo se guarda como trazabilidad,
    /// nunca bloquea la aprobación. Agenda la purga automática a 30 días (spec confirmada).
    /// </summary>
    public Result Approve(string decidedByUserId, string decidedByName, string createdUserId, IClock clock, bool viaQuickAction = false)
    {
        if (Status != ApplicationStatus.Pending)
        {
            return Result.Failure(Error.Conflict("AdmissionApplication.NotPending", "La solicitud ya fue decidida y es inmutable."));
        }

        Status = ApplicationStatus.Approved;
        Decision = new ApplicationDecision(clock.UtcNow, decidedByUserId, decidedByName, null);
        CreatedUserId = createdUserId;
        ApprovedViaQuickAction = viaQuickAction;
        PurgeScheduledAtUtc = clock.UtcNow.AddDays(30);
        Raise(new AdmissionApplicationApproved(Guid.NewGuid(), clock.UtcNow, Id, decidedByUserId, decidedByName));
        return Result.Success();
    }

    /// <summary>RN-06. Agenda la purga automática a 30 días (spec confirmada: igual que al aprobar).</summary>
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
        PurgeScheduledAtUtc = clock.UtcNow.AddDays(30);
        Raise(new AdmissionApplicationRejected(Guid.NewGuid(), clock.UtcNow, Id, decidedByUserId, reason.Trim()));
        return Result.Success();
    }
}
