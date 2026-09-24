using Shekinah.Domain.Billing;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Abstractions;

/// <summary>
/// Puertos declarados en Application, implementados en Infrastructure (DIP, spec técnico §4-D).
/// Interfaces pequeñas y de propósito único (ISP): un handler que solo necesita la hora depende de
/// <see cref="Domain.Common.IClock"/>, no de una fachada gigante.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainPassword);

    bool Verify(string plainPassword, string hash);
}

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(string userId, int enrollmentNumber, UserRole role, string? regionId);

    (string RefreshToken, DateTime ExpiresAtUtc) CreateRefreshToken();
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    int? EnrollmentNumber { get; }

    UserRole? Role { get; }

    string? RegionId { get; }
}

/// <summary>RN-09: fuerza el filtro por región para RegionalCoordinator en el handler, nunca en el cliente.</summary>
public interface IRegionScopeResolver
{
    /// <summary>Devuelve el regionId obligatorio si el usuario actual es RegionalCoordinator; null si no aplica restricción.</summary>
    string? ResolveMandatoryRegionId();
}

/// <summary>Adjunto de correo (plan de control escolar, fase 8: Kardex en PDF). El contenido viaja
/// completo en memoria — los documentos que genera este sistema (ficha, Kardex) son de unas pocas
/// páginas, muy lejos de cualquier límite práctico de outbox/SMTP.</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailSender
{
    /// <summary>
    /// <paramref name="cc"/> es opcional y solo debe usarse en correos administrativos (RN-04):
    /// nunca en correos personales del alumno (credenciales, avisos de morosidad), para no filtrar
    /// datos de un alumno hacia otra bandeja. <paramref name="attachments"/> es opcional (fase 8:
    /// el Kardex se envía con su PDF adjunto; el resto de correos del sistema no lo usan).
    /// </summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct, string? cc = null, IReadOnlyList<EmailAttachment>? attachments = null);
}

/// <summary>
/// Puerto hacia la configuración de notificaciones (implementado en Infrastructure sobre
/// EmailSettings), para que Application pueda pedir la copia administrativa sin conocer Infrastructure.
/// </summary>
public interface INotificationRecipients
{
    /// <summary>Dirección que se copia (Cc) en correos administrativos como RN-04; puede ser null.</summary>
    string? AdministrativeCc { get; }
}

public interface IEnrollmentNumberGenerator
{
    Task<EnrollmentNumber> NextAsync(CancellationToken ct);

    Task EnsureSequenceAtLeastAsync(int minimumValue, CancellationToken ct);
}

/// <summary>
/// Puerto hacia la matrícula "amigable" ITS/{Abreviatura de región}/{consecutivo} (ej. "ITS/SM/00001"),
/// generada al aprobar una solicitud — aditiva a <see cref="IEnrollmentNumberGenerator"/>, no lo
/// reemplaza (ver comentario en <c>User.Matricula</c>). Un contador atómico independiente POR REGIÓN
/// (mismo patrón findAndModify que <see cref="IEnrollmentNumberGenerator"/>), para que cada sede
/// tenga su propio consecutivo.
/// </summary>
public interface IMatriculaGenerator
{
    Task<string> NextAsync(string regionAbbreviation, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}

public interface IFileStorage
{
    Task<string> SaveAsync(string fileName, string contentType, Stream content, CancellationToken ct);

    Task<Stream> OpenAsync(string fileId, CancellationToken ct);
}

public sealed record SpreadsheetRow(int RowNumber, IReadOnlyDictionary<string, string> Values);

/// <summary>O/C abierto/cerrado: agregar .ods = nueva implementación, cero cambios en el handler (spec técnico §4-O).</summary>
public interface ISpreadsheetReader
{
    bool CanRead(string fileName);

    IAsyncEnumerable<SpreadsheetRow> ReadAsync(Stream content, string fileName, CancellationToken ct);
}

public interface IPaymentMatrixReader
{
    Task<Shekinah.Application.Abstractions.PagedResult<StudentPaymentRow>> GetMatrixAsync(PaymentMatrixFilter filter, CancellationToken ct);
}

public sealed record PaymentMatrixFilter(string PeriodId, string? RegionId, int Page, int PageSize);

public sealed record StudentPaymentRow(string StudentId, int EnrollmentNumber, string StudentName, string RegionName, string Email, string Phone, IReadOnlyDictionary<string, bool> PaidByMonth, IReadOnlyList<string> MonthsDue);

/// <summary>Fase 8 (Kardex): fila de materia dentro del documento PDF institucional.</summary>
public sealed record KardexPdfSubjectRow(string SubjectName, int? TermNumber, int? Grade, string Status, string PeriodCode);

/// <summary>Modelo plano que alimenta al generador de PDF (puerto en Application, implementado con
/// QuestPDF en Infrastructure — el dominio/aplicación no conoce la librería de PDF concreta).</summary>
public sealed record KardexPdfModel(
    string FolioOrEnrollment, string StudentFullName, string Email, string? RegionName, string? Modality,
    int? CurrentTerm, DateTime EnrolledAtUtc, bool IsGraduated, double? AverageGrade,
    IReadOnlyList<KardexPdfSubjectRow> Subjects, DateTime GeneratedAtUtc);

/// <summary>Puerto hacia el generador de PDF institucional (DIP): Application pide "un PDF de este
/// Kardex" sin saber si por debajo hay QuestPDF, wkhtmltopdf o cualquier otra librería.</summary>
public interface IKardexPdfGenerator
{
    byte[] Generate(KardexPdfModel model);
}

/// <summary>Fase 7 (Admisiones): una fila etiqueta/valor de la ficha de inscripción en PDF —
/// mismo criterio que <see cref="KardexPdfSubjectRow"/>, para no acoplar Application a la forma en
/// que el dominio de Admisiones modela sus value objects.</summary>
public sealed record AdmissionFichaPdfRow(string Label, string Value);

/// <summary>Modelo plano que alimenta al generador de PDF de la ficha de inscripción (DIP, mismo
/// patrón que <see cref="KardexPdfModel"/>/<see cref="IKardexPdfGenerator"/>).</summary>
public sealed record AdmissionFichaPdfModel(
    string Folio, string FullName, string Email, DateTime GeneratedAtUtc, IReadOnlyList<AdmissionFichaPdfRow> Rows);

/// <summary>Puerto hacia el generador de PDF de la ficha de inscripción (DIP): Application no sabe
/// si por debajo hay QuestPDF ni de dónde sale el logo institucional.</summary>
public interface IAdmissionFichaPdfGenerator
{
    byte[] Generate(AdmissionFichaPdfModel model);
}

/// <summary>Panel de verificación de pagos (docs/Plan-Panel-Pagos.md, fase 1): una fila etiqueta/valor
/// del recibo de pago en PDF — mismo criterio que <see cref="AdmissionFichaPdfRow"/>.</summary>
public sealed record PaymentReceiptPdfRow(string Label, string Value);

/// <summary>Modelo plano que alimenta al generador de PDF del recibo de pago (DIP, mismo patrón que
/// <see cref="AdmissionFichaPdfModel"/>/<see cref="IAdmissionFichaPdfGenerator"/>).</summary>
public sealed record PaymentReceiptPdfModel(
    string Folio, string StudentFullName, string EnrollmentNumber, DateTime GeneratedAtUtc, IReadOnlyList<PaymentReceiptPdfRow> Rows);

/// <summary>Puerto hacia el generador de PDF del recibo de pago (DIP): Application no sabe que por
/// debajo hay QuestPDF.</summary>
public interface IPaymentReceiptPdfGenerator
{
    byte[] Generate(PaymentReceiptPdfModel model);
}

/// <summary>
/// Puerto hacia la verificación de reCAPTCHA v3 (DIP): protege el endpoint anónimo de solicitud de
/// admisión (RN-01, [AllowAnonymousUseCase]) contra spam/bots. Application no sabe que por debajo
/// hay una llamada HTTP a Google — solo pide "¿este token de este action es de un humano?".
/// </summary>
public interface IRecaptchaVerifier
{
    /// <summary><paramref name="expectedAction"/> debe coincidir con el "action" que reCAPTCHA v3
    /// registró al generar el token en el cliente, además de superar el umbral de score configurado
    /// (Recaptcha:MinimumScore) — así un token robado/reusado de otra acción del sitio no sirve aquí.</summary>
    Task<bool> VerifyAsync(string token, string expectedAction, CancellationToken ct);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    /// <summary>Solo para GetMatrixAsync: columnas de mes materializadas por AcademicPeriodMonthsCalculator (RN-17).</summary>
    public IReadOnlyList<string> Months { get; init; } = [];
}
