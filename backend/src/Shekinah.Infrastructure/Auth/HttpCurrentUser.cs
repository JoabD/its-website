using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId => Principal?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

    public int? EnrollmentNumber => int.TryParse(Principal?.FindFirstValue("enrollmentNumber"), out var v) ? v : null;

    public UserRole? Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;

    public string? RegionId => Principal?.FindFirstValue("regionId");
}

/// <summary>RN-09: fuerza el filtro por región para RegionalCoordinator, en el servidor, siempre.</summary>
public sealed class RegionScopeResolver(ICurrentUser currentUser) : IRegionScopeResolver
{
    public string? ResolveMandatoryRegionId() => currentUser.Role == UserRole.RegionalCoordinator ? currentUser.RegionId : null;
}
