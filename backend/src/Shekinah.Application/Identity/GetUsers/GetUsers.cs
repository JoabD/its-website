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

/// <summary><paramref name="ExtraRoles"/>/<paramref name="TeachingRegionNames"/> (2026-10, "un
/// usuario puede tener múltiples roles" + "un profesor puede ser asignado a varias regiones"): el
/// frontend los usa para mostrar, en "Docentes" y en el selector de "Agregar materia", a alguien
/// que da clases sin que Teacher sea su rol principal, y en qué regiones.</summary>
public sealed record UserListItem(
    string Id, int EnrollmentNumber, string? Matricula, string FullName, string? Email, string? Phone, UserRole Role, string Status,
    string? RegionName, Modality? Modality, int? CurrentTerm, string? Plan, IReadOnlyList<UserRole> ExtraRoles, IReadOnlyList<string> TeachingRegionNames);

public sealed class GetUsersQueryHandler(Domain.Identity.IUserRepository users, IRegionScopeResolver regionScope) : IQueryHandler<GetUsersQuery, PagedResult<UserListItem>>
{
    public async Task<Result<PagedResult<UserListItem>>> HandleAsync(GetUsersQuery query, CancellationToken ct)
    {
        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        var effectiveRegionId = mandatoryRegionId ?? query.RegionId;

        // Nota: cuando query.Role no es null, IUserRepository.SearchAsync ya hace coincidir tanto el
        // rol principal como ExtraRoles (ver UserRepository.SearchAsync) — así "Docentes" (Role=Teacher)
        // también trae a quien da clases como rol adicional sin serlo como rol principal.
        var (items, total) = await users.SearchAsync(query.Role, effectiveRegionId, query.SearchText, query.Page, query.PageSize, ct);

        var mapped = items.Select(u => new UserListItem(
            u.Id, u.EnrollmentNumber.Value, u.Matricula, u.Profile.FullName.FullName, u.Profile.Email?.Value, u.Profile.Phone?.Value, u.Role,
            u.Status.ToString(), u.Region?.Name, u.Modality, u.Academic?.CurrentTerm.Value, u.Academic?.Plan.ToString(),
            u.ExtraRoles, u.TeachingRegions.Select(r => r.Name).ToList())).ToList();

        return Result.Success(new PagedResult<UserListItem>(mapped, query.Page, query.PageSize, total));
    }
}
