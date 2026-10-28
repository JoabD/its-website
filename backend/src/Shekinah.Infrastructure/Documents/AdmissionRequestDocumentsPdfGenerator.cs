using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Shekinah.Application.Abstractions;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Infrastructure.Documents;

/// <summary>Genera formatos imprimibles de inscripción y recomendación pastoral para el aspirante.</summary>
public sealed class AdmissionRequestDocumentsPdfGenerator : IAdmissionRequestDocumentsPdfGenerator
{
    private static readonly Color PrimaryColor = Color.FromHex("#1a2744");
    private static readonly Color AccentColor = Color.FromHex("#c8a250");
    private static readonly byte[] LogoBytes = LoadLogo();

    static AdmissionRequestDocumentsPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] GenerateEnrollmentRequest(string applicantName, Modality modality) => GenerateDocument(
        "SOLICITUD DE INGRESO AL INSTITUTO TEOLÓGICO SHEKINAH",
        content =>
        {
            content.Item().PaddingTop(20).Text("A: David Gutiérrez Torres").Bold();
            content.Item().Text("Director del Instituto Teológico Shekinah");

            content.Item().PaddingTop(22).Text("Por medio de la presente YO,");
            content.Item().PaddingTop(5).Background(Colors.Grey.Lighten4).Padding(8).Text(applicantName).Bold().FontSize(12);
            content.Item().AlignCenter().Text("(nombre del alumno)").FontSize(8).FontColor(Colors.Grey.Darken1);

            content.Item().PaddingTop(16).Text(text =>
            {
                text.Span("solicito la inscripción al ");
                text.Span("INSTITUTO TEOLÓGICO SHEKINAH.").Bold();
            });
            content.Item().PaddingTop(14).Text($"En su modalidad:   Presencial ({Check(modality == Modality.Onsite)})   Virtual ({Check(modality == Modality.Online)})   Diplomado ({Check(modality == Modality.Diploma)})");

            content.Item().PaddingTop(24).Text(
                "Me comprometo durante el tiempo que dure el periodo de estudios del INSTITUTO TEOLÓGICO SHEKINAH, " +
                "a aprovechar las clases que se impartan, mantener el respeto con mis compañeros y maestros, " +
                "y acatar el reglamento de la institución.")
                .LineHeight(1.5f);

            content.Item().PaddingTop(58).AlignCenter().Text("____________________________________________");
            content.Item().AlignCenter().Text("Nombre y firma del aspirante").Bold();
        });

    public byte[] GeneratePastoralRecommendation(string applicantName) => GenerateDocument(
        "RECOMENDACIÓN PASTORAL",
        content =>
        {
            content.Item().PaddingTop(14).Text("LUGAR Y FECHA: __________________________________________________________");

            content.Item().PaddingTop(20).Text("A: Pastor David Gutiérrez Torres").Bold();
            content.Item().Text("Director del Instituto Teológico Shekinah");

            content.Item().PaddingTop(22).Text("Por este medio expreso a usted, YO, el Pastor");
            content.Item().PaddingTop(4).Text("_________________________________________________________________________");
            content.Item().Text("de la iglesia _____________________________________________________________,");
            content.Item().Text("del área # __________, que emito la presente RECOMENDACIÓN a:");
            content.Item().PaddingTop(5).Background(Colors.Grey.Lighten4).Padding(8).Text(applicantName).Bold().FontSize(12);
            content.Item().PaddingTop(12).Text(
                "quien solicita el ingreso al INSTITUTO TEOLÓGICO SHEKINAH, dando fe de que es miembro " +
                "de esta iglesia y que ha mostrado madurez, capacidad y carácter de alguien que aspira al servicio ministerial.")
                .LineHeight(1.5f);

            content.Item().PaddingTop(55).AlignCenter().Text("____________________________________________");
            content.Item().AlignCenter().Text("Nombre y firma del pastor").Bold();
        });

    private static byte[] GenerateDocument(string title, Action<ColumnDescriptor> composeContent)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(42);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Helvetica").FontColor(Colors.Grey.Darken4));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.ConstantItem(54).Height(54).Image(LogoBytes).FitArea();
                        row.ConstantItem(12);
                        row.RelativeItem().Column(identity =>
                        {
                            identity.Item().Text("Instituto Teológico Shekinah").FontSize(15).Bold().FontColor(PrimaryColor);
                            identity.Item().Text("Formación Bíblica y Ministerial").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingTop(9).LineHorizontal(1.5f).LineColor(AccentColor);
                });

                page.Content().PaddingVertical(20).Column(content =>
                {
                    content.Item().AlignCenter().Text(title).FontSize(15).Bold().FontColor(PrimaryColor);
                    composeContent(content);
                });

                page.Footer().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
        });

        return document.GeneratePdf();
    }

    private static string Check(bool selected) => selected ? "X" : " ";

    private static byte[] LoadLogo()
    {
        var assembly = typeof(AdmissionRequestDocumentsPdfGenerator).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("shekina-logo.png", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No se encontró el recurso embebido del logo institucional.");
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
