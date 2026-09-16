using System.Runtime.CompilerServices;
using ClosedXML.Excel;
using Shekinah.Application.Abstractions;

namespace Shekinah.Infrastructure.Spreadsheets;

/// <summary>
/// Lee .xlsx (ClosedXML) y .csv (parser propio, sin dependencias adicionales). Agregar un formato
/// nuevo (ej. .ods) = nueva implementación de ISpreadsheetReader, cero cambios en el handler
/// (spec técnico §4-O, Open/Closed).
/// </summary>
public sealed class SpreadsheetReader : ISpreadsheetReader
{
    public bool CanRead(string fileName) =>
        fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    public async IAsyncEnumerable<SpreadsheetRow> ReadAsync(Stream content, string fileName, [EnumeratorCancellation] CancellationToken ct)
    {
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            await foreach (var row in ReadCsvAsync(content, ct))
            {
                yield return row;
            }
        }
        else
        {
            foreach (var row in ReadXlsx(content))
            {
                ct.ThrowIfCancellationRequested();
                yield return row;
            }
        }
    }

    private static async IAsyncEnumerable<SpreadsheetRow> ReadCsvAsync(Stream content, [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(content);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null) yield break;

        var headers = headerLine.Split(',').Select(h => h.Trim().Trim('"')).ToArray();
        var rowNumber = 1;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = line.Split(',').Select(v => v.Trim().Trim('"')).ToArray();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length && i < values.Length; i++)
            {
                dict[headers[i]] = values[i];
            }

            yield return new SpreadsheetRow(rowNumber, dict);
        }
    }

    private static IEnumerable<SpreadsheetRow> ReadXlsx(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var worksheet = workbook.Worksheets.First();
        var headerRow = worksheet.Row(1);
        var headers = headerRow.CellsUsed().Select(c => c.GetString().Trim()).ToArray();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = worksheet.Row(r);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length; c++)
            {
                dict[headers[c]] = row.Cell(c + 1).GetString().Trim();
            }

            yield return new SpreadsheetRow(r, dict);
        }
    }
}
