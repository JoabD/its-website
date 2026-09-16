namespace Shekinah.Infrastructure.Outbox;

/// <summary>
/// Configuración de envío de correo (RN-04/05/06/19). Usa Gmail SMTP con una contraseña de
/// aplicación (no la contraseña real de la cuenta) — ver PROMPT-MAESTRO.md §3-bis. El valor real de
/// <see cref="SenderAppPassword"/> vive solo en appsettings.Development.json (git-ignored) o en las
/// variables de entorno del panel de hosting (R8); nunca en un archivo versionado.
/// </summary>
public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderAppPassword { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = "Instituto Teológico Shekinah";

    /// <summary>
    /// Copia (Cc) que se añade solo a correos administrativos (p. ej. RN-04: nueva solicitud a
    /// administradores). NUNCA se añade a correos personales del alumno (credenciales, avisos de
    /// morosidad) — sería una fuga de datos personales de un alumno hacia otro correo.
    /// </summary>
    public string? DefaultCcAddress { get; set; }
}
