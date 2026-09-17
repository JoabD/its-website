using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Seeding;

/// <summary>
/// Seeders idempotentes (spec técnico §5.8): 4 regiones, 28 materias, un periodo activo inicial y
/// un administrador semilla con mustChangePassword=true y contraseña por variable de entorno
/// (NUNCA hardcodeada — R8).
/// </summary>
public sealed class DatabaseSeeder(
    IRegionRepository regions, ISubjectRepository subjects, IAcademicPeriodRepository periods,
    IUserRepository users, IEnrollmentNumberGenerator enrollmentNumbers, IPasswordHasher passwordHasher,
    IClock clock, IConfiguration configuration, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var regionIds = await SeedRegionsAsync(ct);
        await SeedSubjectsAsync(ct);
        await SeedInitialPeriodAsync(ct);
        await SeedAdminUserAsync(regionIds, ct);
    }

    private async Task<Dictionary<Modality, Region>> SeedRegionsAsync(CancellationToken ct)
    {
        var existing = await regions.GetActiveAsync(ct);
        if (existing.Count > 0)
        {
            return existing.GroupBy(r => r.ModalityScope.First()).ToDictionary(g => g.Key, g => g.First());
        }

        // Nombres reconstruidos de inscripcion.js/REGION legado (PROMPT-MAESTRO.md §11, ADR "Regiones").
        var seedData = new (int Code, string Name, Modality Modality)[]
        {
            (1, "Región Centro", Modality.Onsite),
            (2, "Región Norte", Modality.Onsite),
            (3, "Región Virtual", Modality.Online),
            (4, "Región Diplomado", Modality.Diploma),
        };

        var created = new Dictionary<Modality, Region>();
        foreach (var (code, name, modality) in seedData)
        {
            var region = Region.Create(EntityId.NewId(), code, name, [modality]).Value;
            await regions.AddAsync(region, ct);
            created.TryAdd(modality, region);
        }

        logger.LogInformation("Seed: {Count} regiones creadas.", seedData.Length);
        return created;
    }

    private async Task SeedSubjectsAsync(CancellationToken ct)
    {
        var existing = await subjects.GetCurriculumAsync(ct);
        if (existing.Count > 0) return;

        // Plan de estudios exacto (PROMPT-MAESTRO.md §5.9 / spec técnico §5.9).
        var quarterly = new (int Term, string[] Names)[]
        {
            (1, ["Bibliología", "Introducción a la Teología", "Pentateuco", "Historia Eclesiástica"]),
            (2, ["Homilética", "Teología Sistemática II", "Hermenéutica", "Evangelios Sinópticos"]),
            (3, ["Teología Sistemática", "Sermón Expositivo", "Hechos de los Apóstoles", "Liderazgo"]),
            (4, ["Teología Sistemática IV", "Escatología", "Epístolas Paulinas", "Libros Sapienciales"]),
            (5, ["Ejercicios ministeriales", "Teología Sistemática V", "Evangelismo", "Libros Históricos"]),
            (6, ["Evangelio de Juan", "Apologética", "Consejería pastoral", "Ética ministerial"]),
        };

        var order = 1;
        foreach (var (term, names) in quarterly)
        {
            foreach (var name in names)
            {
                var code = $"T{term}-{order:D2}";
                var subject = Subject.CreateQuarterly(EntityId.NewId(), code, name, TermNumber.Create(term).Value, order).Value;
                await subjects.AddAsync(subject, ct);
                order++;
            }
        }

        var diplomaSubjects = new[] { "Eclesiología", "Apocalipsis", "Neumatología", "Administración pastoral" };
        foreach (var name in diplomaSubjects)
        {
            var subject = Subject.CreateDiploma(EntityId.NewId(), $"DIP-{order:D2}", name, order).Value;
            await subjects.AddAsync(subject, ct);
            order++;
        }

        logger.LogInformation("Seed: 28 materias creadas (24 cuatrimestrales + 4 de diplomado).");
    }

    private async Task SeedInitialPeriodAsync(CancellationToken ct)
    {
        var active = await periods.GetActiveAsync(ct);
        if (active is not null) return;

        var now = clock.UtcNow;
        var dateRange = DateRange.Create(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(4).AddDays(-1)).Value;
        var code = PeriodCode.Create($"{now:yyyyMM}01").Value;

        var period = AcademicPeriod.Open(EntityId.NewId(), code, $"Periodo inicial {now:MMMM yyyy}", dateRange, "seed", clock).Value;
        await periods.AddAsync(period, ct);

        logger.LogInformation("Seed: periodo activo inicial creado ({Code}).", code.Value);
    }

    /// <summary>
    /// Cuentas maestras (Administrator) del panel de control escolar: ven y administran todo,
    /// incluyendo la creación de avisos y calendario (plan de reestructuración admin, fase 2).
    /// Seed idempotente POR CORREO (no por "ya existe algún admin"), a propósito: así, si esta app
    /// ya tenía un administrador semilla previo con otro correo (p. ej. el placeholder anterior
    /// admin@its-shekinah.edu.mx), estas tres cuentas se siguen creando igual en el próximo arranque,
    /// tanto en local como en la base ya desplegada en Azure.
    /// </summary>
    private static readonly (string Name, string Email)[] MasterAccounts =
    [
        ("Administración ITS", "admin@institutoteologicoshekinah.com"),
        ("Academia ITS", "academia@institutoteologicoshekinah.com"),
        ("Dirección General", "dagu_pres@hotmail.com"),
    ];

    private async Task SeedAdminUserAsync(Dictionary<Modality, Region> regionsByModality, CancellationToken ct)
    {
        var seedPassword = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            logger.LogWarning("Seed:AdminPassword no está configurada; se omite la creación de cuentas maestras.");
            return;
        }

        foreach (var (name, email) in MasterAccounts)
        {
            var existing = await users.GetByEmailAsync(email, ct);
            if (existing is not null) continue;

            var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
            var address = Address.Create("Sede central", "Centro", "N/D", "N/D", "N/D").Value;
            var church = ChurchInfo.Create("N/A", address, "N/A", "N/A", MinistryRole.None).Value;
            var education = EducationLevel.Create(SchoolingLevel.Other, "N/A").Value;
            var profile = PersonalProfile.Create(
                PersonName.Create(name).Value, Email.Create(email).Value,
                PhoneNumber.Create("5500000000").Value, new DateOnly(1980, 1, 1), null, address, church, education, null, null).Value;

            // mustChangePassword queda en true por defecto (Credentials.CreateTemporary) — cada
            // cuenta maestra debe fijar su propia contraseña real en el primer login (RN-24).
            var admin = User.CreateStaff(EntityId.NewId(), enrollmentNumber, UserRole.Administrator, profile, null, passwordHasher.Hash(seedPassword), clock).Value;
            await users.AddAsync(admin, ct);

            logger.LogInformation("Seed: cuenta maestra creada ({Email}, matrícula {EnrollmentNumber}).", email, enrollmentNumber.Value);
        }
    }
}
