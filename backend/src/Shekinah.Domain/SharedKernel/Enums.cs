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
