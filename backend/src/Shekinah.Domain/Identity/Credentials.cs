using Shekinah.Domain.Common;

namespace Shekinah.Domain.Identity;

/// <summary>
/// Credenciales del usuario. El hash llega ya calculado desde Application (vía IPasswordHasher,
/// puerto hacia Infrastructure/BCrypt) — el dominio nunca hashea ni compara contraseñas en texto plano.
/// </summary>
public sealed record Credentials
{
    public string PasswordHash { get; private init; }

    public bool MustChangePassword { get; init; }

    public DateTime PasswordUpdatedAtUtc { get; private init; }

    public int FailedAttempts { get; private init; }

    public DateTime? LockedUntilUtc { get; private init; }

    private Credentials(string passwordHash, bool mustChangePassword, DateTime passwordUpdatedAtUtc, int failedAttempts, DateTime? lockedUntilUtc)
    {
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
        PasswordUpdatedAtUtc = passwordUpdatedAtUtc;
        FailedAttempts = failedAttempts;
        LockedUntilUtc = lockedUntilUtc;
    }

    /// <summary>RN-05: contraseña temporal, cambio obligatorio en el primer login (RN-24).</summary>
    public static Result<Credentials> CreateTemporary(string passwordHash, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<Credentials>(Error.Validation("Credentials.Empty", "El hash de la contraseña es requerido."));
        }

        return Result.Success(new Credentials(passwordHash, mustChangePassword: true, clock.UtcNow, 0, null));
    }

    /// <summary>Reconstrucción desde el migrador: conserva el hash bcrypt legado tal cual (spec técnico §5.10).</summary>
    public static Result<Credentials> CreateMigrated(string bcryptHash, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(bcryptHash))
        {
            return Result.Failure<Credentials>(Error.Validation("Credentials.Empty", "El hash migrado no puede estar vacío."));
        }

        return Result.Success(new Credentials(bcryptHash, mustChangePassword: false, clock.UtcNow, 0, null));
    }

    /// <summary>Reconstrucción íntegra desde Infrastructure (mapeo Mongo) o desde el migrador.</summary>
    public static Credentials Rehydrate(string passwordHash, bool mustChangePassword, DateTime passwordUpdatedAtUtc, int failedAttempts, DateTime? lockedUntilUtc) =>
        new(passwordHash, mustChangePassword, passwordUpdatedAtUtc, failedAttempts, lockedUntilUtc);

    public Credentials WithNewPassword(string newHash, IClock clock) =>
        new(newHash, mustChangePassword: false, clock.UtcNow, 0, null);

    public Credentials WithFailedAttempt(IClock clock, int maxAttempts, TimeSpan lockDuration)
    {
        var attempts = FailedAttempts + 1;
        var lockedUntil = attempts >= maxAttempts ? clock.UtcNow.Add(lockDuration) : LockedUntilUtc;
        return this with { FailedAttempts = attempts, LockedUntilUtc = lockedUntil };
    }

    public Credentials WithSuccessfulLogin() => this with { FailedAttempts = 0, LockedUntilUtc = null };

    public bool IsTemporarilyLocked(IClock clock) => LockedUntilUtc is not null && LockedUntilUtc > clock.UtcNow;
}
