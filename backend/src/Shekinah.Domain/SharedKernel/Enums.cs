namespace Shekinah.Domain.SharedKernel;

/// <summary>
/// Modalidad de estudio. Mutuamente excluyente por solicitud/alumno (RN-02). Se guarda como string
/// legible en Mongo, nunca como número mágico (spec técnico §5.1; el legado usaba "ESTATUS = 2").
/// </summary>
public enum Modality
{
    Onsite,
    Online,
    Diploma,
}

public enum UserRole
{
    Student,
    Teacher,
    Administrator,
    RegionalCoordinator,

    /// <summary>Apoyo operativo del coordinador regional (gestión de alumnos/pagos de su región).
    /// Requiere región asignada, igual que RegionalCoordinator (spec del plan de control escolar, fase 2).</summary>
    RegionalSecretary,
}

public enum UserStatus
{
    Active,
    Blocked,
    Inactive,
}

public enum ApplicationStatus
{
    Pending,
    Approved,
    Rejected,
}

public enum AcademicPeriodStatus
{
    Active,
    Closed,
}

public enum ProgramType
{
    Quarterly,
    Diploma,
}

/// <summary>
/// Plan de estudios del ALUMNO (distinto de <see cref="ProgramType"/>, que clasifica materias del
/// currículo): Cuatrimestral o Semestral. Toda solicitud pública aprobada es siempre Quarterly,
/// cuatrimestre 1 (RN de producto) — Semester solo se asigna al dar de alta manualmente o por Excel
/// (plan de control escolar, alta manual de alumnos). Por ahora es solo clasificación/reporte: no
/// determina qué materias se asignan (eso queda para una fase futura, cuando exista currículo
/// semestral en el catálogo).
/// </summary>
public enum StudyPlan
{
    Quarterly,
    Semester,
}

public enum EnrollmentStatus
{
    Active,
    Dropped,
}

public enum SchoolingLevel
{
    Primary,
    Secondary,
    HighSchool,
    Other,
}

public enum NoticeChannel
{
    Email,
}

public enum ImportBatchStatus
{
    Processing,
    Completed,
    Failed,
}
