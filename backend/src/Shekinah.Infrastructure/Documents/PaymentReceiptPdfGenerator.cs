using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Documents;

/// <summary>
/// Panel de verificación de pagos (docs/Plan-Panel-Pagos.md, fase 1): recibo de pago en PDF, mismo
/// membrete institucional que <see cref="AdmissionFichaPdfGenerator"/>/<see cref="KardexPdfGenerator"/>
/// (logo del Shekinah, colores #1a2744/#c8a250) — reutiliza el logo embebido del ensamblado, no un
/// archivo en disco (ver comentario en AdmissionFichaPdfGenerator sobre por qué).
/// </summary>
public sealed class PaymentReceiptPdfGenerator : IPaymentReceiptPdfGenerator
{
    private static readonly QuestPDF.Infrastructure.Color PrimaryColor = QuestPDF.Infrastructure.Color.FromHex("#1a2744");
    private static readonly QuestPDF.Infrastructure.Color AccentColor = QuestPDF.Infrastructure.Color.FromHex("#c8a250");
    private static readonly byte[] LogoBytes = LoadLogo();

    static PaymentReceiptPdfGenerator()
    {
        // Licencia Community de QuestPDF: gratuita para organizaciones con ingresos anuales menores
        // a 1,000,000 USD (mismo criterio que AdmissionFichaPdfGenerator/KardexPdfGenerator).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static byte[] LoadLogo()
    {
        var assembly = typeof(PaymentReceiptPdfGenerator).Assembly;
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("shekina-logo.png", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No se encontró el recurso embebido del logo institucional (shekina-logo.png).");
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public byte[] Generate(PaymentReceiptPdfModel model)
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
                            col.Item().PaddingTop(2).Text("Recibo de pago").FontSize(12).FontColor(AccentColor);
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
                        info.Item().Text(model.StudentFullName).Bold().FontSize(13).FontColor(PrimaryColor);
                        info.Item().Text($"Matrícula: {model.EnrollmentNumber}").FontSize(9).FontColor(Colors.Grey.Darken1);
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
