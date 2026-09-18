namespace Shekinah.Infrastructure.Security;

/// <summary>
/// Configuración de Google reCAPTCHA v3 para el formulario público de inscripción (RN-01). La
/// SiteKey es pública (viaja también al frontend, hardcodeada ahí a propósito — no es secreta por
/// diseño de Google). El valor real de <see cref="SecretKey"/> vive solo en
/// appsettings.Development.json (git-ignored) o en las variables de entorno del panel de hosting
/// (R8); nunca en un archivo versionado — mismo criterio que <c>EmailSettings.SenderAppPassword</c>.
/// </summary>
public sealed class RecaptchaSettings
{
    public const string SectionName = "Recaptcha";

    public string SiteKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Umbral mínimo de score (0.0 = casi seguro un bot, 1.0 = casi seguro humano). 0.5 es el valor
    /// que la propia documentación de Google recomienda como punto de partida.
    /// </summary>
    public double MinimumScore { get; set; } = 0.5;
}
