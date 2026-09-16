using System.Text;

namespace Shekinah.Migrator.Reports;

/// <summary>Reporte de reconciliación: conteos origen vs. destino por colección (requisito §5.10/Fase 8).</summary>
public sealed class ReconciliationReport
{
    private readonly List<(string Collection, int Source, int Mapped, int Rejected)> _rows = [];

    public void AddCollection(string collection, int source, int mapped, int rejected) => _rows.Add((collection, source, mapped, rejected));

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- Reporte de reconciliación ---");
        sb.AppendLine($"{"Colección",-30}{"Origen",10}{"Mapeados",10}{"Rechazados",12}");
        foreach (var (collection, source, mapped, rejected) in _rows)
        {
            sb.AppendLine($"{collection,-30}{source,10}{mapped,10}{rejected,12}");
        }
        return sb.ToString();
    }
}
