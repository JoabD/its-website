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

public interface IEmailSender
{
    /// <summary>
    /// <paramref name="cc"/> es opcional y solo debe usarse en correos administrativos (RN-04):
    /// nunca en correos personales del alumno (credenciales, avisos de morosidad), para no filtrar
    /// datos de un alumno hacia otra bandeja.
    /// </summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct, string? cc = null);
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

public sealed record StudentPaymentRow(string StudentId, int EnrollmentNumber, string StudentName, string RegionName, IReadOnlyDictionary<string, bool> PaidByMonth, IReadOnlyList<string> MonthsDue);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    /// <summary>Solo para GetMatrixAsync: columnas de mes materializadas por AcademicPeriodMonthsCalculator (RN-17).</summary>
    public IReadOnlyList<string> Months { get; init; } = [];
}
