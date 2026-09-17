using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Shekinah.Infrastructure.Auth;

namespace Shekinah.Api.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// Hallazgo de auditoría #1: en la API nueva todo endpoint es [Authorize] por defecto
    /// (fallback policy). El acceso anónimo es una excepción explícita vía RequireAuthorization()
    /// omitido + [AllowAnonymousUseCase] en el Command/Query correspondiente (AuthorizationBehavior).
    /// </summary>
    public static IServiceCollection AddShekinahAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var signingKey = jwtSection["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey no configurada.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // BUG REAL encontrado en producción: por defecto, JwtBearerHandler REMAPEA el claim
                // "sub" del token a ClaimTypes.NameIdentifier (un URI largo distinto) al validarlo.
                // JwtTokenService.CreateAccessToken emite el claim como JwtRegisteredClaimNames.Sub
                // ("sub"), y HttpCurrentUser.UserId lo vuelve a buscar por ese mismo nombre corto
                // ("sub") — pero como el handler ya lo remapeó, esa búsqueda SIEMPRE devolvía null,
                // para CUALQUIER usuario autenticado, en todo el sistema. La mayoría de los handlers
                // no se notaba porque usan "currentUser.UserId ?? "system"" como fallback silencioso
                // (por ejemplo en avisos, pagos, calendario); pero /auth/change-password hace
                // "currentUser.UserId!" sin ese fallback, así que ObjectId.Parse(null) explotaba con
                // una excepción no controlada → 500 "Failure.Unexpected" (justo el que reportó el
                // usuario al intentar cambiar su contraseña). Desactivar el remapeo automático hace
                // que el claim llegue tal cual se emitió, consistente con lo que todo el código ya
                // esperaba.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
