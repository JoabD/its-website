using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.GetUsers;

/// <summary>
/// RN-09: además de Administrator (ve todo), un RegionalCoordinator/RegionalSecretary puede
/// consultar usuarios — pero SIEMPRE acotado a su propia región, forzado en el handler vía
/// <see cref="IRegionScopeResolver"/> (plan de control escolar, fase 5: separar Alumnos/Docentes
/// reutilizando este mismo query filtrado por Role).
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record GetUsersQuery(UserRole? Role, string? RegionId, string? SearchText, int Page, int PageSize) : IQuery<PagedResult<UserListItem>>;

public sealed record UserListItem(string Id, int EnrollmentNumber, string? Matricula, string FullName, string Email, string Phone, UserRole Role, string Status, string? RegionName, Modality? Modality, int? CurrentTerm, string? Plan);

public sealed class GetUsersQueryHandler(Domain.Identity.IUserRepository users, IRegionScopeResolver regionScope) : IQueryHandler<GetUsersQuery, PagedResult<UserListItem>>
{
    public async Task<Result<PagedResult<UserListItem>>> HandleAsync(GetUsersQuery query, CancellationToken ct)
    {
        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        var effectiveRegionId = mandatoryRegionId ?? query.RegionId;

        var (items, total) = await users.SearchAsync(query.Role, effectiveRegionId, query.SearchText, query.Page, query.PageSize, ct);

        var mapped = items.Select(u => new UserListItem(
            u.Id, u.EnrollmentNumber.Value, u.Matricula, u.Profile.FullName.FullName, u.Profile.Email.Value, u.Profile.Phone.Value, u.Role,
            u.Status.ToString(), u.Region?.Name, u.Modality, u.Academic?.CurrentTerm.Value, u.Academic?.Plan.ToString())).ToList();

        return Result.Success(new PagedResult<UserListItem>(mapped, query.Page, query.PageSize, total));
    }
}
