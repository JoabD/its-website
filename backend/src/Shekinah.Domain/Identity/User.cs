using Shekinah.Domain.Common;
using Shekinah.Domain.Identity.Events;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Identity;

public sealed record RegionRef(string Id, int Code, string Name);

/// <summary>
/// Agregado raíz del contexto Identity. Protege: matrícula única e inmutable; un
/// <see cref="UserRole.RegionalCoordinator"/> DEBE tener región; un <see cref="UserRole.Student"/>
/// debe tener modalidad y cuatrimestre; bloqueo automático al 4.º aviso (RN-07/RN-19);
/// <c>mustChangePassword</c> obligatorio (RN-24). Nótese: NO hay herencia <c>Student : User</c>
/// (prohibido explícitamente, spec técnico §4-L) — el rol es un valor, no un subtipo.
///
/// <see cref="ExtraRoles"/> (2026-10, pedido explícito del cliente: "un usuario puede tener
/// múltiples roles") es la ÚNICA excepción a "un solo rol": permite marcar que alguien TAMBIÉN
/// puede dar clases sin dejar de ser su rol principal (p. ej. un Administrador que también es
/// profesor). <see cref="Role"/> sigue siendo el único rol que determina menús/autorización general
/// — <see cref="ExtraRoles"/> solo habilita las capacidades específicas de Profesor (aparecer en el
/// selector de "Agregar materia", pasar RequireRole(Teacher, ...), calificar sus propias ofertas).
/// </summary>
public sealed class User : AggregateRoot<string>
{
    private User() { }

    private User(
        string id, EnrollmentNumber enrollmentNumber, string? matricula, UserRole role, Credentials credentials,
        RegionRef? region, Modality? modality, AcademicState? academic, PersonalProfile profile,
        string? admissionApplicationId, IClock clock, IReadOnlyList<RegionRef>? teachingRegions = null)
        : base(id)
    {
        EnrollmentNumber = enrollmentNumber;
        Matricula = matricula;
        Role = role;
        Credentials = credentials;
        Region = region;
        Modality = modality;
        Academic = academic;
        Billing = BillingState.Clean;
        Profile = profile;
        Status = UserStatus.Active;
        AdmissionApplicationId = admissionApplicationId;
        CreatedAtUtc = clock.UtcNow;
        TeachingRegions = teachingRegions ?? [];
    }

    public EnrollmentNumber EnrollmentNumber { get; private set; } = null!;

    /// <summary>
    /// Matrícula "amigable" con formato ITS/{Abreviatura de región}/{consecutivo}, ej. "ITS/SM/00001"
    /// — SOLO para alumnos aprobados desde una solicitud (<see cref="CreateStudentFromApplication"/>);
    /// null para el resto de roles y para cuentas legado. Es aditiva a propósito: <see cref="EnrollmentNumber"/>
    /// (el entero heredado del sistema legado) sigue siendo la matrícula interna real — login (JWT),
    /// kardex y facturación siguen usándola tal cual, sin ningún cambio. Esta es solo la que se
    /// muestra/entrega al alumno.
    /// </summary>
    public string? Matricula { get; private set; }

    public UserRole Role { get; private set; }

    /// <summary>Roles ADICIONALES al principal (ver comentario de clase). Hoy solo se usa para
    /// Teacher; se modela como lista (no un solo bool "EsTambiénProfesor") para no tener que volver
    /// a tocar el dominio si en el futuro se agrega otra combinación.</summary>
    public IReadOnlyList<UserRole> ExtraRoles { get; private set; } = [];

    public UserStatus Status { get; private set; }

    public Credentials Credentials { get; private set; } = null!;

    /// <summary>Obligatorio para Student y RegionalCoordinator; null para Teacher/Administrator.</summary>
    public RegionRef? Region { get; private set; }

    /// <summary>Solo aplica a Student.</summary>
    public Modality? Modality { get; private set; }

    /// <summary>Solo aplica a Student.</summary>
    public AcademicState? Academic { get; private set; }

    public BillingState Billing { get; private set; } = BillingState.Clean;

    public PersonalProfile Profile { get; private set; } = null!;

    public string? AdmissionApplicationId { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Regiones donde esta persona da clases (2026-10, pedido explícito del cliente: "un profesor
    /// puede ser asignado a varias regiones y tener múltiples materias"). A diferencia de
    /// <see cref="Region"/> (singular, exclusivo de Student/RegionalCoordinator/RegionalSecretary),
    /// esto es una LISTA — relevante cuando <see cref="CanTeach"/> es true, ya sea porque
    /// <see cref="Role"/> es Teacher o porque Teacher está en <see cref="ExtraRoles"/>. No reemplaza
    /// ni restringe la asignación real de materias (CourseOffering ya permite un profesor en
    /// cualquier región vía AssignTeacherCommand) — es la lista que se le muestra al administrador
    /// al elegir quién puede dar clases en dónde.
    /// </summary>
    public IReadOnlyList<RegionRef> TeachingRegions { get; private set; } = [];

    /// <summary>True si esta persona puede dar clases, ya sea como rol principal o como rol
    /// adicional (ver <see cref="ExtraRoles"/>).</summary>
    public bool CanTeach => Role == UserRole.Teacher || ExtraRoles.Contains(UserRole.Teacher);

    /// <summary>RN-05: creación de alumno a partir de una solicitud aprobada, en la misma transacción.</summary>
    public static Result<User> CreateStudentFromApplication(
        string id, EnrollmentNumber enrollmentNumber, string matricula, string admissionApplicationId, PersonalProfile profile,
        Modality modality, RegionRef region, string temporaryPasswordHash, IClock clock)
    {
        var credentialsResult = Credentials.CreateTemporary(temporaryPasswordHash, clock);
        if (credentialsResult.IsFailure)
        {
            return Result.Failure<User>(credentialsResult.Error);
        }

        var user = new User(
            id, enrollmentNumber, matricula, UserRole.Student, credentialsResult.Value,
            region, modality, AcademicState.Start(clock), profile, admissionApplicationId, clock);

        user.Raise(new UserCreated(Guid.NewGuid(), clock.UtcNow, id, enrollmentNumber.Value, nameof(UserRole.Student)));
        return Result.Success(user);
    }

    /// <summary>
    /// Alta MANUAL de un alumno (plan de control escolar): a diferencia de <see cref="CreateStudentFromApplication"/>,
    /// no viene de una solicitud — el administrador captura los datos directamente (formulario o
    /// importación por Excel) y decide plan (Cuatrimestral/Semestral) y en qué cuatrimestre/semestre
    /// entra, típicamente para alumnos que ya venían cursando fuera del sistema. Igual que una
    /// solicitud aprobada: matrícula "amigable" (<see cref="Matricula"/>) + contraseña temporal con
    /// cambio obligatorio en el primer login.
    /// </summary>
    public static Result<User> CreateStudentManually(
        string id, EnrollmentNumber enrollmentNumber, string matricula, PersonalProfile profile,
        Modality modality, RegionRef region, StudyPlan plan, TermNumber currentTerm, string temporaryPasswordHash, IClock clock)
    {
        var credentialsResult = Credentials.CreateTemporary(temporaryPasswordHash, clock);
        if (credentialsResult.IsFailure)
        {
            return Result.Failure<User>(credentialsResult.Error);
        }

        var academicResult = AcademicState.StartManual(plan, currentTerm, clock);
        if (academicResult.IsFailure)
        {
            return Result.Failure<User>(academicResult.Error);
        }

        var user = new User(
            id, enrollmentNumber, matricula, UserRole.Student, credentialsResult.Value,
            region, modality, academicResult.Value, profile, null, clock);

        user.Raise(new UserCreated(Guid.NewGuid(), clock.UtcNow, id, enrollmentNumber.Value, nameof(UserRole.Student)));
        return Result.Success(user);
    }

    /// <summary>Alta administrativa de cualquier rol (RN-12: solo Administrator puede invocar este caso de uso).
    /// <paramref name="teachingRegions"/> solo aplica cuando <paramref name="role"/> es Teacher (ver
    /// <see cref="TeachingRegions"/>) — para el resto de roles se ignora.</summary>
    public static Result<User> CreateStaff(
        string id, EnrollmentNumber enrollmentNumber, UserRole role, PersonalProfile profile,
        RegionRef? region, string temporaryPasswordHash, IClock clock, IReadOnlyList<RegionRef>? teachingRegions = null)
    {
        if (role == UserRole.Student)
        {
            return Result.Failure<User>(Error.Validation("User.UseStudentFactory", "Use CreateStudentFromApplication para alumnos."));
        }

        if (role is UserRole.RegionalCoordinator or UserRole.RegionalSecretary && region is null)
        {
            return Result.Failure<User>(Error.Validation("User.RegionRequired", "Un coordinador o secretario regional debe tener una región asignada."));
        }

        var credentialsResult = Credentials.CreateTemporary(temporaryPasswordHash, clock);
        if (credentialsResult.IsFailure)
        {
            return Result.Failure<User>(credentialsResult.Error);
        }

        var user = new User(
            id, enrollmentNumber, null, role, credentialsResult.Value, region, null, null, profile, null, clock,
            teachingRegions: role == UserRole.Teacher ? teachingRegions : null);
        user.Raise(new UserCreated(Guid.NewGuid(), clock.UtcNow, id, enrollmentNumber.Value, role.ToString()));
        return Result.Success(user);
    }

    /// <summary>Reconstrucción desde Infrastructure (mapeo Mongo) o desde el migrador.</summary>
    public static User Rehydrate(
        string id, EnrollmentNumber enrollmentNumber, UserRole role, UserStatus status, Credentials credentials,
        RegionRef? region, Modality? modality, AcademicState? academic, BillingState billing, PersonalProfile profile,
        string? admissionApplicationId, DateTime? lastLoginAtUtc, DateTime createdAtUtc, int version, string? matricula = null,
        IReadOnlyList<UserRole>? extraRoles = null, IReadOnlyList<RegionRef>? teachingRegions = null)
    {
        var user = new User
        {
            Id = id,
            EnrollmentNumber = enrollmentNumber,
            Matricula = matricula,
            Role = role,
            Status = status,
            Credentials = credentials,
            Region = region,
            Modality = modality,
            Academic = academic,
            Billing = billing,
            Profile = profile,
            AdmissionApplicationId = admissionApplicationId,
            LastLoginAtUtc = lastLoginAtUtc,
            CreatedAtUtc = createdAtUtc,
            Version = version,
            ExtraRoles = extraRoles ?? [],
            TeachingRegions = teachingRegions ?? [],
        };
        return user;
    }

    /// <summary>RN-07: motivo textual de bloqueo por login, sin exponer si la matrícula existe.</summary>
    public bool CanAuthenticate(out string? blockReason)
    {
        blockReason = null;

        if (Status == UserStatus.Inactive)
        {
            blockReason = "Usuario inactivo.";
            return false;
        }

        if (Status == UserStatus.Blocked || Billing.IsBlocked)
        {
            blockReason = "Usuario bloqueado por falta de pago.";
            return false;
        }

        return true;
    }

    public void RegisterSuccessfulLogin(IClock clock)
    {
        Credentials = Credentials.WithSuccessfulLogin();
        LastLoginAtUtc = clock.UtcNow;
    }

    public void RegisterFailedLogin(IClock clock, int maxAttempts, TimeSpan lockDuration) =>
        Credentials = Credentials.WithFailedAttempt(clock, maxAttempts, lockDuration);

    public Result ChangePassword(string newHash, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(newHash))
        {
            return Result.Failure(Error.Validation("User.InvalidPassword", "La nueva contraseña no es válida."));
        }

        Credentials = Credentials.WithNewPassword(newHash, clock);
        return Result.Success();
    }

    public void ResetPassword(string temporaryHash, string resetByUserId, IClock clock)
    {
        Credentials = Credentials.WithNewPassword(temporaryHash, clock) with { MustChangePassword = true };
        Raise(new UserPasswordReset(Guid.NewGuid(), clock.UtcNow, Id, resetByUserId));
    }

    /// <summary>RN-19: registra un aviso; bloquea automáticamente al superar 3.</summary>
    public void RegisterPaymentNotice(IClock clock)
    {
        Billing = Billing.RegisterNotice(clock);
        if (Billing.IsBlocked && Status != UserStatus.Blocked)
        {
            Status = UserStatus.Blocked;
            Raise(new UserBlockedForDelinquency(Guid.NewGuid(), clock.UtcNow, Id, Billing.NoticeCount));
        }
    }

    /// <summary>RN-18: la importación de pagos reinicia el contador y desbloquea.</summary>
    public void ResetDelinquency()
    {
        Billing = Billing.ResetAfterPayment();
        if (Status == UserStatus.Blocked)
        {
            Status = UserStatus.Active;
        }
    }

    /// <summary>RN-14: promoción de cuatrimestre al cerrar un periodo (solo aplica a Student activos).</summary>
    public Result PromoteTerm(IClock clock)
    {
        if (Role != UserRole.Student || Academic is null)
        {
            return Result.Failure(Error.Validation("User.NotAStudent", "Solo un alumno tiene cuatrimestre para promover."));
        }

        Academic = Academic.Promote(clock);
        return Result.Success();
    }

    /// <summary>RN-23: editar el perfil personal NUNCA toca el expediente de admisión.</summary>
    public void UpdateProfile(PersonalProfile profile) => Profile = profile;

    public Result UpdateRoleAndScope(UserRole role, RegionRef? region, Modality? modality)
    {
        if (role is UserRole.RegionalCoordinator or UserRole.RegionalSecretary && region is null)
        {
            return Result.Failure(Error.Validation("User.RegionRequired", "Un coordinador o secretario regional debe tener una región asignada."));
        }

        Role = role;
        Region = role is UserRole.Student or UserRole.RegionalCoordinator or UserRole.RegionalSecretary ? region : null;
        Modality = role == UserRole.Student ? modality : null;
        return Result.Success();
    }

    /// <summary>
    /// "Docentes" → "Ya tiene cuenta" (2026-10, pedido explícito del cliente: "no necesitamos
    /// revocar ni un rol... un usuario puede tener múltiples roles"). Agrega Teacher como rol
    /// ADICIONAL (ver <see cref="ExtraRoles"/>) sin tocar <see cref="Role"/> — a propósito distinto
    /// de <see cref="UpdateRoleAndScope"/>, que SÍ reemplaza el rol principal. Fusiona
    /// <paramref name="regions"/> con las que ya tuviera (sin duplicar) en vez de reemplazarlas, para
    /// que asignarlo de nuevo con una región distinta vaya SUMANDO regiones, no sustituyéndolas.
    /// </summary>
    public void AddTeachingRole(IReadOnlyList<RegionRef> regions)
    {
        if (Role != UserRole.Teacher && !ExtraRoles.Contains(UserRole.Teacher))
        {
            ExtraRoles = [.. ExtraRoles, UserRole.Teacher];
        }

        var merged = TeachingRegions.ToList();
        foreach (var region in regions)
        {
            if (!merged.Any(r => r.Id == region.Id))
            {
                merged.Add(region);
            }
        }

        TeachingRegions = merged;
    }

    public void Deactivate() => Status = UserStatus.Inactive;

    public void Reactivate()
    {
        if (!Billing.IsBlocked)
        {
            Status = UserStatus.Active;
        }
    }
}
