using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.Announcements;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Announcements.CreateAnnouncement;

/// <summary>
/// Plan de control escolar, fase 6: publicar avisos institucionales es EXCLUSIVO de cuentas
/// maestras (Administrator) — confirmado por el usuario. Al publicarse, se envía por correo a
/// todos los docentes activos (confirmado: "deben llegar por correo a los correos de cada
/// docente"); a futuro (no en este entregable) se añadiría un canal de WhatsApp sin tocar este
/// caso de uso, siguiendo el mismo patrón desacoplado que ya usa <see cref="NoticeChannel"/> en
/// cobranza.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record CreateAnnouncementCommand(string Title, string Body) : ICommand<CreateAnnouncementResponse>;

public sealed record CreateAnnouncementResponse(string AnnouncementId, int TeachersNotified);

public sealed class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public sealed class CreateAnnouncementCommandHandler(
    IAnnouncementRepository announcements, IUserRepository users, IEmailSender emailSender,
    ICurrentUser currentUser, IClock clock)
    : ICommandHandler<CreateAnnouncementCommand, CreateAnnouncementResponse>
{
    private const int MaxTeachersPerBroadcast = 1000;

    public async Task<Result<CreateAnnouncementResponse>> HandleAsync(CreateAnnouncementCommand command, CancellationToken ct)
    {
        var announcementResult = Announcement.Publish(EntityId.NewId(), command.Title, command.Body, currentUser.UserId ?? "system", clock);
        if (announcementResult.IsFailure)
        {
            return Result.Failure<CreateAnnouncementResponse>(announcementResult.Error);
        }

        var announcement = announcementResult.Value;
        var (teachers, _) = await users.SearchAsync(UserRole.Teacher, null, null, 1, MaxTeachersPerBroadcast, ct);

        var notified = 0;
        foreach (var teacher in teachers)
        {
            if (teacher.Status != UserStatus.Active)
            {
                continue;
            }

            // Correo personal de staff, sin Cc administrativo (mismo criterio que el resto de
            // correos individuales del sistema).
            await emailSender.SendAsync(
                teacher.Profile.Email.Value,
                $"Aviso — {announcement.Title}",
                $"<p>{System.Net.WebUtility.HtmlEncode(announcement.Body).Replace("\n", "<br/>")}</p><p style=\"color:#8994a8;font-size:12px;\">Instituto Teológico Shekinah — panel de control escolar.</p>",
                ct);

            notified++;
        }

        if (notified > 0)
        {
            announcement.MarkEmailedToTeachers();
        }

        await announcements.AddAsync(announcement, ct);

        return Result.Success(new CreateAnnouncementResponse(announcement.Id, notified));
    }
}
