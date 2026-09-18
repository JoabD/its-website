using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.CreateUser;

/// <summary>RN-12: solo Administrator administra usuarios. Alta de cualquier rol.</summary>
[RequireRole(UserRole.Administrator)]
public sealed record CreateUserCommand(
    UserRole Role, string FullName, string Email, string Phone, DateOnly BirthDate,
    string? RegionId, Modality? Modality) : ICommand<CreateUserResponse>;

public sealed record CreateUserResponse(string UserId, int EnrollmentNumber, string TemporaryPassword);

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.RegionId).NotEmpty().When(x => x.Role is UserRole.RegionalCoordinator or UserRole.RegionalSecretary);
    }
}

public sealed class CreateUserCommandHandler(
    IUserRepository users, Domain.Catalog.IRegionRepository regions, IEnrollmentNumberGenerator enrollmentNumbers,
    IPasswordHasher passwordHasher, IClock clock)
    : ICommandHandler<CreateUserCommand, CreateUserResponse>
{
    public async Task<Result<CreateUserResponse>> HandleAsync(CreateUserCommand command, CancellationToken ct)
    {
        var nameResult = PersonName.Create(command.FullName);
        var emailResult = Email.Create(command.Email);
        var phoneResult = PhoneNumber.Create(command.Phone);

        if (nameResult.IsFailure) return Result.Failure<CreateUserResponse>(nameResult.Error);
        if (emailResult.IsFailure) return Result.Failure<CreateUserResponse>(emailResult.Error);
        if (phoneResult.IsFailure) return Result.Failure<CreateUserResponse>(phoneResult.Error);

        RegionRef? region = null;
        if (!string.IsNullOrWhiteSpace(command.RegionId))
        {
            var regionEntity = await regions.GetByIdAsync(command.RegionId, ct);
            if (regionEntity is null)
            {
                return Result.Failure<CreateUserResponse>(Error.Validation("CreateUser.InvalidRegion", "La región indicada no existe."));
            }

            region = new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name);
        }

        var address = Address.Create("N/D", "N/D", "N/D", "N/D").Value;
        var church = ChurchInfo.Create("N/D", address, "N/D", "N/D", MinistryRole.None).Value;
        var education = EducationLevel.Create(SchoolingLevel.Other, "N/D").Value;

        var profileResult = PersonalProfile.Create(
            nameResult.Value, emailResult.Value, phoneResult.Value, command.BirthDate, null, address, church, education, null, null);
        if (profileResult.IsFailure) return Result.Failure<CreateUserResponse>(profileResult.Error);

        var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var userResult = User.CreateStaff(
            EntityId.NewId(), enrollmentNumber, command.Role, profileResult.Value, region,
            passwordHasher.Hash(temporaryPassword), clock);

        if (userResult.IsFailure) return Result.Failure<CreateUserResponse>(userResult.Error);

        await users.AddAsync(userResult.Value, ct);
        return Result.Success(new CreateUserResponse(userResult.Value.Id, enrollmentNumber.Value, temporaryPassword));
    }
}

/// <summary>RN-05: contraseña temporal aleatoria ≥ 12 caracteres, alfanumérica segura.</summary>
public static class TemporaryPasswordGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public static string Generate(int length = 12)
    {
        Span<byte> buffer = stackalloc byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);
        return string.Create(length, buffer.ToArray(), (span, bytes) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Chars[bytes[i] % Chars.Length];
            }
        });
    }
}
