using System.Net;
using FluentValidation;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Billing.RegisterManualPayment;
using Shekinah.Domain.Billing;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Billing.SendPaymentReceipt;

/// <summary>
/// Panel de verificación de pagos (docs/Plan-Panel-Pagos.md, fase 1): al verificar un pago, el
/// admin/coordinador/secretario regional puede generar el recibo en PDF y enviarlo. El correo se
/// manda de una vez, automático (reutiliza <see cref="IEmailSender"/> con adjunto, mismo patrón que
/// <c>SendKardexEmail</c>). Para WhatsApp, la Opción C decidida por el instituto (sin presupuesto
/// para la API oficial de Meta ni riesgo de una integración no oficial): el backend arma el número
/// y el texto del mensaje, y el frontend abre el enlace `wa.me` — un clic humano en vez de cero,
/// pero sin redactar nada a mano.
/// El PDF SIEMPRE se genera y se devuelve para descargar, sin importar si hay correo o teléfono —
/// el envío por correo se omite si el alumno no tiene correo, y los datos de WhatsApp vienen en
/// null si no tiene teléfono (mismo ajuste de flujo real que el alta de alumnos: ninguno de los
/// dos es obligatorio ya).
/// RN-09: mismo chequeo de alcance regional que RegisterManualPayment/UndoManualPayment.
/// </summary>
[RequireRole(UserRole.Administrator, UserRole.RegionalCoordinator, UserRole.RegionalSecretary)]
public sealed record SendPaymentReceiptCommand(string StudentId, string MonthCode) : ICommand<SendPaymentReceiptResponse>;

/// <summary>
/// <paramref name="PdfBase64"/>/<paramref name="FileName"/> (agregado tras feedback real: "un botón
/// que envíe por correo Y descargue el recibo") van en la MISMA respuesta que ya generaba el PDF una
/// vez — evita generarlo dos veces (una para el correo, otra para la descarga).
/// </summary>
public sealed record SendPaymentReceiptResponse(string? SentTo, string? WhatsAppPhone, string? WhatsAppMessage, string PdfBase64, string FileName);

public sealed class SendPaymentReceiptCommandValidator : AbstractValidator<SendPaymentReceiptCommand>
{
    public SendPaymentReceiptCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.MonthCode).NotEmpty();
    }
}

public sealed class SendPaymentReceiptCommandHandler(
    IUserRepository users, IPaymentRepository payments, IRegionScopeResolver regionScope,
    IPaymentReceiptPdfGenerator pdfGenerator, IEmailSender emailSender, IClock clock)
    : ICommandHandler<SendPaymentReceiptCommand, SendPaymentReceiptResponse>
{
    public async Task<Result<SendPaymentReceiptResponse>> HandleAsync(SendPaymentReceiptCommand command, CancellationToken ct)
    {
        var monthCodeResult = MonthCode.Create(command.MonthCode);
        if (monthCodeResult.IsFailure)
        {
            return Result.Failure<SendPaymentReceiptResponse>(monthCodeResult.Error);
        }

        var student = await users.GetByIdAsync(command.StudentId, ct);
        if (student is null || student.Role != UserRole.Student)
        {
            return Result.Failure<SendPaymentReceiptResponse>(Error.NotFound("SendPaymentReceipt.StudentNotFound", "El alumno indicado no existe."));
        }

        var mandatoryRegionId = regionScope.ResolveMandatoryRegionId();
        if (mandatoryRegionId is not null && student.Region?.Id != mandatoryRegionId)
        {
            return Result.Failure<SendPaymentReceiptResponse>(Error.Forbidden("SendPaymentReceipt.OutOfScope", "No puede generar recibos de alumnos fuera de su región."));
        }

        var payment = await payments.FindAsync(student.Id, monthCodeResult.Value, ct);
        if (payment is null)
        {
            return Result.Failure<SendPaymentReceiptResponse>(Error.NotFound("SendPaymentReceipt.NotPaid", "Ese mes no tiene un pago registrado todavía."));
        }

        var amount = payment.Amount?.Amount ?? RegisterManualPaymentCommandHandler.FixedMonthlyQuota;
        var folio = $"REC-{student.EnrollmentNumber.Value}-{monthCodeResult.Value.Value}";
        var generatedAt = clock.UtcNow;

        var pdfModel = new PaymentReceiptPdfModel(
            folio, student.Profile.FullName.FullName, student.EnrollmentNumber.Value.ToString(), generatedAt,
            [
                new PaymentReceiptPdfRow("Alumno", student.Profile.FullName.FullName),
                new PaymentReceiptPdfRow("Matrícula", student.Matricula ?? student.EnrollmentNumber.Value.ToString()),
                new PaymentReceiptPdfRow("Región", student.Region?.Name ?? "N/D"),
                new PaymentReceiptPdfRow("Mes pagado", FormatMonth(monthCodeResult.Value.Value)),
                new PaymentReceiptPdfRow("Monto", $"${amount:N2} {payment.Amount?.Currency ?? "MXN"}"),
                new PaymentReceiptPdfRow("Fecha de verificación", payment.RegisteredAtUtc.ToString("dd/MM/yyyy HH:mm")),
            ]);

        var pdfBytes = pdfGenerator.Generate(pdfModel);
        var fileName = $"Recibo-{folio}.pdf";

        var bodyHtml = $"""
            <p>Hola {WebUtility.HtmlEncode(student.Profile.FullName.FullName)},</p>
            <p>Confirmamos tu pago de <strong>{FormatMonth(monthCodeResult.Value.Value)}</strong> por <strong>${amount:N2} MXN</strong>. Adjunto encontrarás tu recibo.</p>
            <p style="color:#8994a8;font-size:12px;">Instituto Teológico Shekinah — panel de control escolar.</p>
            """;

        // Sin correo registrado (ajuste de flujo real), no hay a quién enviarle el recibo por
        // correo — el PDF de todos modos se genera y se puede descargar/enviar por WhatsApp.
        if (student.Profile.Email is not null)
        {
            await emailSender.SendAsync(
                student.Profile.Email.Value, $"Recibo de pago — {FormatMonth(monthCodeResult.Value.Value)} — Instituto Teológico Shekinah", bodyHtml, ct,
                attachments: [new EmailAttachment(fileName, "application/pdf", pdfBytes)]);
        }

        // wa.me espera el número completo con lada de país (52 = México) sin signos ni espacios —
        // PhoneNumber ya garantiza 10 dígitos limpios (ver Domain.SharedKernel.PhoneNumber). Sin
        // teléfono registrado (mismo ajuste de flujo real que el correo), simplemente no hay número
        // que armar — el frontend ya construye su propio enlace wa.me con el teléfono del alumno,
        // así que estos campos quedan en null y la opción de WhatsApp no se ofrece.
        string? whatsAppPhone = null;
        string? whatsAppMessage = null;
        if (student.Profile.Phone is not null)
        {
            whatsAppPhone = $"52{student.Profile.Phone.Value}";
            var emailNote = student.Profile.Email is not null ? " Tu recibo también fue enviado a tu correo." : string.Empty;
            whatsAppMessage =
                $"Hola {student.Profile.FullName.FullName}, confirmamos tu pago de {FormatMonth(monthCodeResult.Value.Value)} por ${amount:N2} MXN.{emailNote} " +
                "— Instituto Teológico Shekinah";
        }

        return Result.Success(new SendPaymentReceiptResponse(
            student.Profile.Email?.Value, whatsAppPhone, whatsAppMessage, Convert.ToBase64String(pdfBytes), fileName));
    }

    private static string FormatMonth(string monthCode)
    {
        var year = monthCode[..4];
        var month = int.Parse(monthCode[4..]);
        var names = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
        return $"{names[month - 1]} {year}";
    }
}
