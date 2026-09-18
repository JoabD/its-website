using System.Linq;
using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Documents;

/// <summary>
/// Genera la "ficha de inscripción" en PDF con el membrete institucional (logo del Shekinah,
/// colores #1a2744/#c8a250 — misma base visual que <see cref="KardexPdfGenerator"/>). Sustituye/
/// complementa el HTML embebido de <c>AdmissionFichaHtml</c> (Application) como adjunto elegante en
/// los correos de <c>SubmitApplication</c>, tanto para administración como para el solicitante.
///
/// El logo viaja embebido como recurso del ensamblado (ver .csproj), no como archivo en disco: así
/// no depende de wwwroot ni del working directory del proceso en producción.
/// </summary>
public sealed class AdmissionFichaPdfGenerator : IAdmissionFichaPdfGenerator
{
    private static readonly QuestPDF.Infrastructure.Color PrimaryColor = QuestPDF.Infrastructure.Color.FromHex("#1a2744");
    private static readonly QuestPDF.Infrastructure.Color AccentColor = QuestPDF.Infrastructure.Color.FromHex("#c8a250");
    private static readonly byte[] LogoBytes = LoadLogo();

    static AdmissionFichaPdfGenerator()
    {
        // Licencia Community de QuestPDF: gratuita para organizaciones con ingresos anuales menores
        // a 1,000,000 USD (caso del Instituto) — ver KardexPdfGenerator, mismo criterio.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Busca el recurso embebido por sufijo en vez de exigir el nombre exacto: el nombre lógico que
    /// MSBuild genera para un <c>&lt;EmbeddedResource&gt;</c> depende del RootNamespace + la ruta de
    /// carpetas del proyecto, así que emparejar por sufijo evita que un cambio de estructura de
    /// carpetas rompa esto en silencio (o en producción) por una constante desincronizada.
    /// </summary>
    private static byte[] LoadLogo()
    {
        var assembly = typeof(AdmissionFichaPdfGenerator).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("shekina-logo.png", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "No se encontró el recurso embebido del logo institucional (shekina-logo.png). " +
                "Verifique el <EmbeddedResource> en Shekinah.Infrastructure.csproj y que Documents/Assets/shekina-logo.png exista.");
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public byte[] Generate(AdmissionFichaPdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.ConstantItem(56).Height(56).Image(LogoBytes).FitArea();
                        row.ConstantItem(12);
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Instituto Teológico Shekinah").FontSize(16).Bold().FontColor(PrimaryColor);
                            col.Item().Text("Formación Bíblica y Ministerial").FontSize(9).FontColor(Colors.Grey.Darken1);
                            col.Item().PaddingTop(2).Text("Ficha de inscripción").FontSize(12).FontColor(AccentColor);
                        });
                        row.ConstantItem(150).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Folio: {model.Folio}").FontSize(9).Bold();
                            col.Item().Text($"Generado: {model.GeneratedAtUtc:dd/MM/yyyy HH:mm} (UTC)").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                    header.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor(AccentColor);
                });

                page.Content().PaddingVertical(16).Column(content =>
                {
                    content.Item().Background(Colors.Grey.Lighten4).Padding(12).Column(info =>
                    {
                        info.Item().Text(model.FullName).Bold().FontSize(13).FontColor(PrimaryColor);
                        info.Item().Text(model.Email).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    content.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        var alternate = false;
                        foreach (var row in model.Rows)
                        {
                            var background = alternate ? Colors.Grey.Lighten5 : Colors.White;
                            table.Cell().Background(background).Padding(6).Text(row.Label).FontSize(9).FontColor(Colors.Grey.Darken2);
                            table.Cell().Background(background).Padding(6).Text(row.Value).FontSize(9.5f).Bold().FontColor(PrimaryColor);
                            alternate = !alternate;
                        }
                    });
                });

                page.Footer().PaddingTop(10).Column(footer =>
                {
                    footer.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    footer.Item().PaddingTop(4).Text("Documento generado automáticamente por el sistema de control escolar del Instituto Teológico Shekinah. No requiere firma autógrafa.")
                        .FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }
}
