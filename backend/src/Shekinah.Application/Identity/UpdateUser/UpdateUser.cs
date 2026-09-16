using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.UpdateUser;

[RequireRole(UserRole.Administrator)]
public sealed record UpdateUserCommand(string UserId, UserRole Role, string? RegionId, Modality? Modality, string? Status) : ICommand<Unit>;

public sealed class UpdateUserCommandHandler(IUserRepository users, Domain.Catalog.IRegionRepository regions)
    : ICommandHandler<UpdateUserCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(UpdateUserCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        RegionRef? region = null;
        if (!string.IsNullOrWhiteSpace(command.RegionId))
        {
            var regionEntity = await regions.GetByIdAsync(command.RegionId, ct);
            if (regionEntity is null)
            {
                return Result.Failure<Unit>(Error.Validation("UpdateUser.InvalidRegion", "La región indicada no existe."));
            }

            region = new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name);
        }

        var result = user.UpdateRoleAndScope(command.Role, region, command.Modality);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        if (command.Status == "Inactive") user.Deactivate();
        if (command.Status == "Active") user.Reactivate();

        await users.UpdateAsync(user, ct);
        return Result.Success(Unit.Value);
    }
}
