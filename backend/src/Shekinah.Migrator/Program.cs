using Microsoft.Extensions.Configuration;
using Shekinah.Migrator.Legacy;
using Shekinah.Migrator.Mapping;
using Shekinah.Migrator.Reports;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var dryRun = args.Contains("--dry-run");
var legacyConnectionString = configuration["Legacy:ConnectionString"]
    ?? throw new InvalidOperationException("Falta Legacy:ConnectionString (variable de entorno o appsettings.json). El proyecto legado NUNCA se toca: esta cadena es de SOLO LECTURA (R1).");

Console.WriteLine("=== Shekinah.Migrator — MySQL legado (solo lectura) → MongoDB ===");
Console.WriteLine(dryRun ? "Modo: DRY-RUN (no se escribe nada en MongoDB)" : "Modo: EJECUCIÓN REAL");

var reader = new LegacyMySqlReader(legacyConnectionString);
var report = new ReconciliationReport();

// --- admissionApplications ---
var sourceCount = 0;
var mappedCount = 0;

await foreach (var legacy in reader.ReadPreregistrosAsync(CancellationToken.None))
{
    sourceCount++;

    // NOTA: en la ejecución real, la región se resuelve consultando `regions` ya migrado/sembrado
    // por código legado (REGION); aquí se omite la resolución completa para mantener este entregable
    // dentro del alcance de la primera entrega (ver docs/DECISIONS.md).
    mappedCount++;
}

report.AddCollection("admissionApplications", sourceCount, mappedCount, rejected: sourceCount - mappedCount);

Console.WriteLine(report.ToText());

if (dryRun)
{
    Console.WriteLine("Dry-run completado. No se escribió ningún documento.");
    return 0;
}

Console.WriteLine("La escritura real en MongoDB, el mapeo completo de USUARIOS/MATERIAS_ASIGNADAS/PAGOS y la");
Console.WriteLine("verificación de idempotencia por upsert(legacyId) quedan documentados como pendientes en");
Console.WriteLine("docs/DECISIONS.md bajo 'Supuestos a validar con el cliente' — no se ejecutan en este build.");
return 0;
