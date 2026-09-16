namespace Shekinah.Infrastructure.Auth;

/// <summary>Nunca hardcodeada (R8): llega por appsettings.Development.json / variables de entorno / user-secrets.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "shekinah-its";

    public string Audience { get; set; } = "shekinah-its-clients";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
