namespace Shekinah.Domain.Admissions;

/// <summary>
/// Ids "conocidos" para los 4 documentos con los que arranca el catálogo (Configuración →
/// Documentos de inscripción). Se comparten entre la migración que los siembra
/// (<c>M004_SeedChecklistItemDefinitions</c>) y el mapeo de compatibilidad hacia atrás en
/// <c>AdmissionApplicationRepository</c> (solicitudes guardadas antes del catálogo editable, con
/// el checklist como 4 booleanos fijos) — así el progreso ya marcado no se pierde al migrar.
/// </summary>
public static class ChecklistItemDefinitionSeed
{
    public const string OfficialIdItemId = "official-id";
    public const string EnrollmentRequestItemId = "enrollment-request";
    public const string PastoralRecommendationItemId = "pastoral-recommendation";
    public const string BaptismCertificateItemId = "baptism-certificate";

    public static readonly IReadOnlyList<(string Id, string Label, int DisplayOrder)> Defaults =
    [
        (OfficialIdItemId, "Identificación oficial", 1),
        (EnrollmentRequestItemId, "Solicitud de inscripción", 2),
        (PastoralRecommendationItemId, "Recomendación pastoral", 3),
        (BaptismCertificateItemId, "Certificado de bautismo", 4),
    ];
}
