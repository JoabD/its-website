using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.UpdateMyProfile;

/// <summary>RN-23: editar la información personal del alumno NUNCA muta el expediente de admisión.</summary>
public sealed record UpdateMyProfileCommand(
    string FullName, string Email, string Phone, DateOnly BirthDate, string MaritalStatus,
    string Street, string Neighborhood, string Locality, string Municipality, string? State) : ICommand<Abstractions.Unit>;

public sealed class UpdateMyProfileCommandHandler(IUserRepository users, ICurrentUser currentUser) : ICommandHandler<UpdateMyProfileCommand, Abstractions.Unit>
{
    public async Task<Result<Abstractions.Unit>> HandleAsync(UpdateMyProfileCommand command, CancellationToken ct)
    {
        if (currentUser.UserId is null)
        {
            return Result.Failure<Abstractions.Unit>(Error.Unauthorized("Auth.Required", "Se requiere autenticación."));
        }

        var user = await users.GetByIdAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return Result.Failure<Abstractions.Unit>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        var name = PersonName.Create(command.FullName);
        var email = Email.Create(command.Email);
        var phone = PhoneNumber.Create(command.Phone);
        var address = Address.Create(command.Street, command.Neighborhood, command.Locality, command.Municipality, command.State);

        if (name.IsFailure) return Result.Failure<Abstractions.Unit>(name.Error);
        if (email.IsFailure) return Result.Failure<Abstractions.Unit>(email.Error);
        if (phone.IsFailure) return Result.Failure<Abstractions.Unit>(phone.Error);
        if (address.IsFailure) return Result.Failure<Abstractions.Unit>(address.Error);

        var updatedProfile = PersonalProfile.Create(
            name.Value, email.Value, phone.Value, command.BirthDate, command.MaritalStatus, address.Value,
            user.Profile.Church, user.Profile.Education, user.Profile.TheologicalBackground, user.Profile.StudyPurpose);

        if (updatedProfile.IsFailure) return Result.Failure<Abstractions.Unit>(updatedProfile.Error);

        user.UpdateProfile(updatedProfile.Value);
        await users.UpdateAsync(user, ct);
        return Result.Success(Abstractions.Unit.Value);
    }
}
