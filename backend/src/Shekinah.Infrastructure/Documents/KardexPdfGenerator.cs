using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Documents;

/// <summary>
/// Plan de control escolar, fase 8: generación del Kardex institucional en PDF, con el mismo
/// formato (membrete, folio/matrícula, sello de "documento generado por el sistema") pensado para
/// reutilizarse también en la ficha de inscripción (fase 7) — de momento cada uno tiene su propio
/// generador porque son documentos distintos, pero comparten esta misma base visual (colores
/// institucionales #1a2744/#c8a250, ver PROMPT-MAESTRO.md).
/// </summary>
public sealed class KardexPdfGenerator : IKardexPdfGenerator
{
    private static readonly QuestPDF.Infrastructure.Color PrimaryColor = QuestPDF.Infrastructure.Color.FromHex("#1a2744");
    private static readonly QuestPDF.Infrastructure.Color AccentColor = QuestPDF.Infrastructure.Color.FromHex("#c8a250");

    static KardexPdfGenerator()
    {
        // Licencia Community de QuestPDF: gratuita para organizaciones con ingresos anuales menores
        // a 1,000,000 USD (caso del Instituto) — ver https://www.questpdf.com/license/.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(KardexPdfModel model)
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
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Instituto Teológico Shekinah").FontSize(16).Bold().FontColor(PrimaryColor);
                            col.Item().Text("Kardex Académico").FontSize(12).FontColor(AccentColor);
                        });
                        row.ConstantItem(160).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Matrícula: {model.FolioOrEnrollment}").FontSize(9);
                            col.Item().Text($"Generado: {model.GeneratedAtUtc:dd/MM/yyyy HH:mm}").FontSize(9);
                        });
                    });
                    header.Item().PaddingTop(8).LineHorizontal(1).LineColor(AccentColor);
                });

                page.Content().PaddingVertical(16).Column(content =>
                {
                    content.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(info =>
                    {
                        info.Item().Text(model.StudentFullName).Bold().FontSize(13);
                        info.Item().Text(model.Email).FontSize(9).FontColor(Colors.Grey.Darken1);
                        info.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text($"Región: {model.RegionName ?? "N/D"}");
                            row.RelativeItem().Text($"Modalidad: {model.Modality ?? "N/D"}");
                            row.RelativeItem().Text($"Cuatrimestre actual: {model.CurrentTerm?.ToString() ?? "N/D"}");
                        });
                        info.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text($"Fecha de ingreso: {model.EnrolledAtUtc:dd/MM/yyyy}");
                            row.RelativeItem().Text($"Estatus: {(model.IsGraduated ? "Egresado" : "Activo")}");
                            row.RelativeItem().Text($"Promedio general: {(model.AverageGrade is null ? "N/D" : model.AverageGrade.Value.ToString("0.0"))}");
                        });
                    });

                    content.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                        });

                        table.Header(headerRow =>
                        {
                            foreach (var title in new[] { "Materia", "Cuatrimestre", "Calificación", "Estatus", "Periodo" })
                            {
                                headerRow.Cell().Background(PrimaryColor).Padding(6)
                                    .Text(title).FontColor(Colors.White).Bold().FontSize(9);
                            }
                        });

                        foreach (var subject in model.Subjects)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(subject.SubjectName).FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(subject.TermNumber?.ToString() ?? "—").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(subject.Grade?.ToString() ?? "—").FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(subject.Status).FontSize(9);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(subject.PeriodCode).FontSize(9);
                        }

                        if (model.Subjects.Count == 0)
                        {
                            table.Cell().ColumnSpan(5).Padding(10).AlignCenter().Text("Sin materias registradas todavía.").FontColor(Colors.Grey.Darken1);
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
