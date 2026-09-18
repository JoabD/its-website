using System.Net;
using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Kardex;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Kardex.SendKardexEmail;

/// <summary>
/// Plan de control escolar, fase 8: "Enviar por correo" del Kardex, con el PDF adjunto desde el
/// arranque (confirmado por el usuario). El alumno solo puede enviarse el suyo a su propio correo
/// (autoservicio, sin Cc — regla de correos personales); Administrator/RegionalCoordinator/
/// RegionalSecretary pueden además indicar un correo distinto (p. ej. para reenviarlo a un tercero
/// al consultar el de un alumno de su alcance).
/// </summary>
[RequireRole(UserRole.Student, UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record SendKardexEmailCommand(string StudentId, string? OverrideEmail) : ICommand<SendKardexEmailResponse>;

public sealed record SendKardexEmailResponse(string SentTo);

public sealed class SendKardexEmailCommandValidator : AbstractValidator<SendKardexEmailCommand>
{
    public SendKardexEmailCommandValidator()
    {
        RuleFor(x => x.OverrideEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.OverrideEmail));
    }
}

public sealed class SendKardexEmailCommandHandler(
    Domain.Identity.IUserRepository users, Domain.Academics.ICourseOfferingRepository offerings,
    IRegionScopeResolver regionScope, ICurrentUser currentUser, IKardexPdfGenerator pdfGenerator,
    IEmailSender emailSender, IClock clock)
    : ICommandHandler<SendKardexEmailCommand, SendKardexEmailResponse>
{
    public async Task<Result<SendKardexEmailResponse>> HandleAsync(SendKardexEmailCommand command, CancellationToken ct)
    {
        var studentResult = await KardexAggregator.ResolveAuthorizedStudentAsync(command.StudentId, users, regionScope, currentUser, ct);
        if (studentResult.IsFailure)
        {
            return Result.Failure<SendKardexEmailResponse>(studentResult.Error);
        }

        var student = studentResult.Value;

        // El alumno SIEMPRE se envía el suyo a su propio correo — un OverrideEmail suyo se ignora
        // a propósito, para que "enviar mi Kardex" nunca termine filtrando datos a otra bandeja.
        var recipient = currentUser.Role == UserRole.Student || string.IsNullOrWhiteSpace(command.OverrideEmail)
            ? student.Profile.Email.Value
            : command.OverrideEmail.Trim();

        var (subjects, average) = await KardexAggregator.BuildSubjectsAsync(student, offerings, ct);

        var pdfModel = new KardexPdfModel(
            student.EnrollmentNumber.Value.ToString(), student.Profile.FullName.FullName, student.Profile.Email.Value,
            student.Region?.Name, student.Modality?.ToString(), student.Academic?.CurrentTerm.Value,
            student.Academic?.EnrolledAtUtc ?? student.CreatedAtUtc, student.Academic?.IsGraduated ?? false,
            average, subjects.Select(s => new KardexPdfSubjectRow(s.SubjectName, s.TermNumber, s.Grade, s.Status, s.PeriodCode)).ToList(),
            clock.UtcNow);

        var pdfBytes = pdfGenerator.Generate(pdfModel);
        var fileName = $"Kardex-{student.EnrollmentNumber.Value}.pdf";

        var bodyHtml = $"""
            <p>Hola {WebUtility.HtmlEncode(student.Profile.FullName.FullName)},</p>
            <p>Adjunto encontrarás tu Kardex académico generado el {clock.UtcNow:dd/MM/yyyy HH:mm}.</p>
            <p style="color:#8994a8;font-size:12px;">Instituto Teológico Shekinah — panel de control escolar.</p>
            """;

        // Correo personal del alumno: sin Cc administrativo, igual que el resto de correos
        // individuales del sistema (credenciales, avisos de morosidad, ficha de inscripción).
        await emailSender.SendAsync(
            recipient, "Tu Kardex académico — Instituto Teológico Shekinah", bodyHtml, ct,
            attachments: [new EmailAttachment(fileName, "application/pdf", pdfBytes)]);

        return Result.Success(new SendKardexEmailResponse(recipient));
    }
}
