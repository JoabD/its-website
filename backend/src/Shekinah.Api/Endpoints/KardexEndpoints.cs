using Shekinah.Api.Extensions;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Kardex.GetStudentKardex;
using Shekinah.Application.Kardex.SendKardexEmail;
using Shekinah.Domain.Common;

namespace Shekinah.Api.Endpoints;

/// <summary>
/// Plan de control escolar, fase 8: Kardex del alumno — consulta, descarga en PDF y envío por
/// correo. <c>{studentId}</c> acepta el literal "me" (el alumno consulta el suyo sin conocer su Id
/// interno); Administrator/RegionalCoordinator/RegionalSecretary pasan el Id real del alumno.
/// </summary>
public static class KardexEndpoints
{
    public static void MapKardexEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/kardex").WithTags("Kardex").RequireAuthorization();

        group.MapGet("/{studentId}", async (string studentId, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.QueryAsync(new GetStudentKardexQuery(studentId), ct)).ToApiResult());

        group.MapGet("/{studentId}/pdf", async (string studentId, IDispatcher dispatcher, IKardexPdfGenerator pdfGenerator, IClock clock, CancellationToken ct) =>
        {
            var result = await dispatcher.QueryAsync(new GetStudentKardexQuery(studentId), ct);
            if (result.IsFailure)
            {
                return result.ToApiResult();
            }

            var kardex = result.Value;
            var pdfModel = new KardexPdfModel(
                kardex.EnrollmentNumber.ToString(), kardex.FullName, kardex.Email, kardex.RegionName,
                kardex.Modality?.ToString(), kardex.CurrentTerm, kardex.EnrolledAtUtc, kardex.IsGraduated,
                kardex.AverageGrade, kardex.Subjects.Select(s => new KardexPdfSubjectRow(s.SubjectName, s.TermNumber, s.Grade, s.Status, s.PeriodCode)).ToList(),
                clock.UtcNow);

            var pdfBytes = pdfGenerator.Generate(pdfModel);
            return Results.File(pdfBytes, "application/pdf", $"Kardex-{kardex.EnrollmentNumber}.pdf");
        });

        group.MapPost("/{studentId}/email", async (string studentId, SendKardexEmailRequest? request, IDispatcher dispatcher, CancellationToken ct) =>
            (await dispatcher.SendAsync(new SendKardexEmailCommand(studentId, request?.Email), ct)).ToApiResult());
    }

    public sealed record SendKardexEmailRequest(string? Email);
}
