using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Auth;

/// <summary>Access token 15 min + refresh 7 días rotativo (PROMPT-MAESTRO.md §3 stack backend).</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(string userId, int enrollmentNumber, UserRole role, string? regionId)
    {
        var opts = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(opts.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("enrollmentNumber", enrollmentNumber.ToString()),
            new(ClaimTypes.Role, role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (!string.IsNullOrWhiteSpace(regionId))
        {
            claims.Add(new Claim("regionId", regionId));
        }

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(opts.Issuer, opts.Audience, claims, expires: expires, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string RefreshToken, DateTime ExpiresAtUtc) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (token, DateTime.UtcNow.AddDays(options.Value.RefreshTokenDays));
    }
}
