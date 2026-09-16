using Shekinah.Application.Abstractions;
using Shekinah.Application.Identity.CreateUser;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.ResetPassword;

[RequireRole(UserRole.Administrator)]
public sealed record ResetPasswordCommand(string UserId) : ICommand<ResetPasswordResponse>;

public sealed record ResetPasswordResponse(string TemporaryPassword);

public sealed class ResetPasswordCommandHandler(IUserRepository users, IPasswordHasher passwordHasher, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    public async Task<Result<ResetPasswordResponse>> HandleAsync(ResetPasswordCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Result.Failure<ResetPasswordResponse>(Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        user.ResetPassword(passwordHasher.Hash(temporaryPassword), currentUser.UserId ?? "system", clock);
        await users.UpdateAsync(user, ct);

        return Result.Success(new ResetPasswordResponse(temporaryPassword));
    }
}
