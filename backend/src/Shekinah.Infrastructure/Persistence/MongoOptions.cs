namespace Shekinah.Infrastructure.Persistence;

/// <summary>
/// Configuración de conexión. NUNCA hardcodeada: llega por appsettings.Development.json
/// (git-ignored), variables de entorno o dotnet user-secrets (R8).
/// </summary>
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "shekinah";
}
