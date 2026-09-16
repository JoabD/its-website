using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;

public sealed record CurrentUserResponse(
    string Id, int EnrollmentNumber, UserRole Role, string FullName, string Email,
    string? RegionName, Modality? Modality, int? CurrentTerm, bool MustChangePassword);

public sealed class GetCurrentUserQueryHandler(Domain.Identity.IUserRepository users, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> HandleAsync(GetCurrentUserQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is null)
        {
            return Result.Failure<CurrentUserResponse>(Error.Unauthorized("Auth.Required", "Se requiere autenticación."));
        }

        var user = await users.GetByIdAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return Result.Failure<CurrentUserResponse>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        return Result.Success(new CurrentUserResponse(
            user.Id, user.EnrollmentNumber.Value, user.Role, user.Profile.FullName.FullName, user.Profile.Email.Value,
            user.Region?.Name, user.Modality, user.Academic?.CurrentTerm.Value, user.Credentials.MustChangePassword));
    }
}
