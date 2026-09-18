using System.Net;
using Shekinah.Domain.Admissions;

namespace Shekinah.Application.Admissions.SubmitApplication;

/// <summary>
/// Renderiza la "ficha de inscripción" como HTML embebible en un correo (RN-01/RN-04, plan de
/// control escolar fase 4). Se comparte entre el correo a administración y el correo de
/// confirmación al solicitante — un único lugar con el formato de la ficha, sin duplicarlo.
///
/// Nota de diseño: por ahora esto es SOLO HTML embebido en el cuerpo del correo, no un PDF adjunto
/// — <see cref="Shekinah.Application.Abstractions.IEmailSender"/> aún no soporta adjuntos. Cuando
/// se construya el generador de PDF institucional para el Kardex (fase 8 del plan), esta misma
/// plantilla se reutiliza para producir el PDF adjunto de la ficha, sin cambiar el resto del flujo.
/// </summary>
internal static class AdmissionFichaHtml
{
    public static string Render(string folio, ApplicantProfile applicant, ModalityChoice modality, DateTime submittedAtUtc)
    {
        string Row(string label, string value) =>
            $"""<tr><td style="padding:4px 12px 4px 0;color:#64748b;white-space:nowrap;">{Encode(label)}</td><td style="padding:4px 0;font-weight:600;color:#101c36;">{Encode(value)}</td></tr>""";

        var modalityLabel = modality.Modality switch
        {
            Domain.SharedKernel.Modality.Onsite => "Presencial",
            Domain.SharedKernel.Modality.Online => "Virtual",
            Domain.SharedKernel.Modality.Diploma => "Diplomado",
            _ => modality.Modality.ToString(),
        };

        var ministryRole = applicant.Church.MinistryRole.HasRole
            ? applicant.Church.MinistryRole.RoleName ?? "Sí"
            : "No";

        return $"""
            <div style="border:1px solid #e2e8f0;border-radius:12px;padding:20px 24px;margin:16px 0;font-family:Arial,sans-serif;">
              <h2 style="margin:0 0 4px 0;color:#101c36;font-size:16px;">Ficha de inscripción — {Encode(folio)}</h2>
              <p style="margin:0 0 16px 0;color:#8994a8;font-size:12.5px;">Generada automáticamente el {submittedAtUtc:dd/MM/yyyy HH:mm} (UTC)</p>
              <table style="border-collapse:collapse;font-size:13.5px;width:100%;">
                {Row("Nombre completo", applicant.FullName.FullName)}
                {Row("Fecha de nacimiento", applicant.BirthDate.ToString("dd/MM/yyyy"))}
                {Row("Estado civil", applicant.MaritalStatus)}
                {Row("Correo", applicant.Email.Value)}
                {Row("Teléfono", applicant.Phone.Value)}
                {Row("Dirección", $"{applicant.Address.Street}, {applicant.Address.Neighborhood}, {applicant.Address.Locality}, {applicant.Address.Municipality}")}
                {Row("Modalidad", modalityLabel)}
                {Row("Región / Sede", modality.RegionName)}
                {Row("Iglesia", applicant.Church.Name)}
                {Row("Pastor", applicant.Church.PastorName)}
                {Row("Tiempo asistiendo", applicant.Church.TimeAttending)}
                {Row("Rol ministerial", ministryRole)}
                {Row("Nivel de estudios", applicant.Education.Level.ToString())}
                {Row("Propósito de estudio", applicant.StudyPurpose)}
              </table>
            </div>
            """;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
