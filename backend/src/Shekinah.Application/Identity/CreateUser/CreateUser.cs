using System.Net;
using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.CreateUser;

/// <summary>RN-12: solo Administrator administra usuarios. Alta de cualquier rol.
/// <paramref name="RegionIds"/> (2026-10, "un profesor puede ser asignado a varias regiones"): solo
/// aplica cuando <paramref name="Role"/> es Teacher — ver <see cref="Domain.Identity.User.TeachingRegions"/>;
/// reemplazó a la fecha de nacimiento como dato obligatorio al agregar un profesor (pedido explícito
/// del cliente), que por eso ahora es opcional.</summary>
[RequireRole(UserRole.Administrator)]
public sealed record CreateUserCommand(
    UserRole Role, string FullName, string? Email, string? Phone, DateOnly? BirthDate,
    string? RegionId, Modality? Modality, IReadOnlyList<string>? RegionIds = null) : ICommand<CreateUserResponse>;

public sealed record CreateUserResponse(string UserId, int EnrollmentNumber, string TemporaryPassword);

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty();
        // Correo y teléfono opcionales (mismo ajuste que CreateStudentCommand): "¿en dónde agregas
        // profesores?" (2026-10) llevó a simplificar el alta de un Profesor a solo nombre + región —
        // el cliente aclaró que correo/teléfono se añadirán después, cuando se construya la función
        // para convertir a un docente en usuario con acceso al sistema. Sin correo, el usuario queda
        // de alta con normalidad pero sin poder iniciar sesión (login es por correo) — igual que ya
        // pasa con un alumno sin correo (ver PersonalProfile.Email).
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.RegionId).NotEmpty().When(x => x.Role is UserRole.RegionalCoordinator or UserRole.RegionalSecretary);
        // Reemplazo de la fecha de nacimiento (2026-10, pedido explícito del cliente): un Profesor
        // nuevo debe quedar asignado a al menos una región desde el alta, no después.
        RuleFor(x => x.RegionIds)
            .Must(ids => ids is { Count: > 0 })
            .When(x => x.Role == UserRole.Teacher)
            .WithMessage("Selecciona al menos una región para el profesor.");
        // User.CreateStaff ya rechaza Role=Student con un mensaje pensado para el desarrollador
        // ("use CreateStudentFromApplication") — aquí se valida antes, con un mensaje que sí tiene
        // sentido para el admin que usa el panel: los alumnos se dan de alta en Alumnos, no aquí.
        RuleFor(x => x.Role).NotEqual(UserRole.Student).WithMessage("Para dar de alta un alumno usa la sección \"Alumnos\".");
    }
}

public sealed class CreateUserCommandHandler(
    IUserRepository users, Domain.Catalog.IRegionRepository regions, IEnrollmentNumberGenerator enrollmentNumbers,
    IPasswordHasher passwordHasher, IEmailSender emailSender, IClock clock)
    : ICommandHandler<CreateUserCommand, CreateUserResponse>
{
    public async Task<Result<CreateUserResponse>> HandleAsync(CreateUserCommand command, CancellationToken ct)
    {
        var nameResult = PersonName.Create(command.FullName);
        if (nameResult.IsFailure) return Result.Failure<CreateUserResponse>(nameResult.Error);

        Email? email = null;
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var emailResult = Email.Create(command.Email);
            if (emailResult.IsFailure) return Result.Failure<CreateUserResponse>(emailResult.Error);
            email = emailResult.Value;

            var existing = await users.GetByEmailAsync(email.Value, ct);
            if (existing is not null)
            {
                return Result.Failure<CreateUserResponse>(Error.Conflict("CreateUser.EmailInUse", "Ya existe un usuario con ese correo."));
            }
        }

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(command.Phone))
        {
            var phoneResult = PhoneNumber.Create(command.Phone);
            if (phoneResult.IsFailure) return Result.Failure<CreateUserResponse>(phoneResult.Error);
            phone = phoneResult.Value;
        }

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

        var teachingRegions = new List<RegionRef>();
        if (command.Role == UserRole.Teacher)
        {
            foreach (var regionId in (command.RegionIds ?? []).Distinct())
            {
                var regionEntity = await regions.GetByIdAsync(regionId, ct);
                if (regionEntity is null)
                {
                    return Result.Failure<CreateUserResponse>(Error.Validation("CreateUser.InvalidRegion", "Una de las regiones indicadas no existe."));
                }

                teachingRegions.Add(new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name));
            }
        }

        var address = Address.Create("N/D", "N/D", "N/D", "N/D").Value;
        var church = ChurchInfo.Create("N/D", address, "N/D", "N/D", MinistryRole.None).Value;
        var education = EducationLevel.Create(SchoolingLevel.Other, "N/D").Value;

        var profileResult = PersonalProfile.Create(
            nameResult.Value, email, phone, command.BirthDate, null, address, church, education, null, null);
        if (profileResult.IsFailure) return Result.Failure<CreateUserResponse>(profileResult.Error);

        var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var userResult = User.CreateStaff(
            EntityId.NewId(), enrollmentNumber, command.Role, profileResult.Value, region,
            passwordHasher.Hash(temporaryPassword), clock, teachingRegions);

        if (userResult.IsFailure) return Result.Failure<CreateUserResponse>(userResult.Error);

        await users.AddAsync(userResult.Value, ct);

        // Pedido explícito del cliente: igual que CreateStudentCommandHandler, se manda por correo
        // la contraseña temporal en texto plano — el admin ya la ve en pantalla al crear, esto es
        // para que el propio usuario nuevo la reciba sin que se la tengan que copiar/pegar a mano.
        // Si no se capturó correo (caso típico de un Profesor recién agregado), simplemente no hay
        // destinatario al que mandarlo — el admin es quien comparte la contraseña manualmente, que
        // para eso se le muestra en pantalla al terminar.
        if (email is not null)
        {
            await emailSender.SendAsync(
                email.Value,
                "Tus credenciales de acceso — Instituto Teológico Shekinah",
                $"""
                <p>Hola {WebUtility.HtmlEncode(nameResult.Value.FullName)},</p>
                <p>Se creó tu cuenta en el sistema del Instituto Teológico Shekinah con el rol de <strong>{command.Role}</strong>.</p>
                <p>Correo: {WebUtility.HtmlEncode(email.Value)}<br>Contraseña temporal: <strong>{WebUtility.HtmlEncode(temporaryPassword)}</strong></p>
                <p>Deberás cambiarla en tu primer inicio de sesión.</p>
                """,
                ct);
        }

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
