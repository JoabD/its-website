using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.GetUsers;

[RequireRole(UserRole.Administrator)]
public sealed record GetUsersQuery(UserRole? Role, string? RegionId, string? SearchText, int Page, int PageSize) : IQuery<PagedResult<UserListItem>>;

public sealed record UserListItem(string Id, int EnrollmentNumber, string FullName, string Email, UserRole Role, string Status, string? RegionName, Modality? Modality, int? CurrentTerm);

public sealed class GetUsersQueryHandler(Domain.Identity.IUserRepository users) : IQueryHandler<GetUsersQuery, PagedResult<UserListItem>>
{
    public async Task<Result<PagedResult<UserListItem>>> HandleAsync(GetUsersQuery query, CancellationToken ct)
    {
        var (items, total) = await users.SearchAsync(query.Role, query.RegionId, query.SearchText, query.Page, query.PageSize, ct);

        var mapped = items.Select(u => new UserListItem(
            u.Id, u.EnrollmentNumber.Value, u.Profile.FullName.FullName, u.Profile.Email.Value, u.Role,
            u.Status.ToString(), u.Region?.Name, u.Modality, u.Academic?.CurrentTerm.Value)).ToList();

        return Result.Success(new PagedResult<UserListItem>(mapped, query.Page, query.PageSize, total));
    }
}
