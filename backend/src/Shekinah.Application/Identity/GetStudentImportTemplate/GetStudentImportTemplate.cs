using Shekinah.Application.Abstractions;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.GetStudentImportTemplate;

/// <summary>
/// Alumnos → "Agregar alumno" → pestaña Excel → "Descargar plantilla" (pedido explícito del cliente,
/// 2026-09: "ocupamos poder descargar directamente de la sección de Alumnos el formato excel para
/// agregar estudiantes" — el texto ya decía "usa la plantilla oficial" pero no existía ningún lugar
/// de dónde bajarla). Mismo rol que ImportStudentsCommand (solo Administrator importa), para no
/// generar una plantilla que el usuario no podría usar de todas formas.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record GetStudentImportTemplateQuery : IQuery<StudentImportTemplateResponse>;

public sealed record StudentImportTemplateResponse(string FileName, string ContentBase64);

public sealed class GetStudentImportTemplateQueryHandler(ISpreadsheetWriter writer, Domain.Catalog.IRegionRepository regions)
    : IQueryHandler<GetStudentImportTemplateQuery, StudentImportTemplateResponse>
{
    public async Task<Result<StudentImportTemplateResponse>> HandleAsync(GetStudentImportTemplateQuery query, CancellationToken ct)
    {
        var activeRegions = await regions.GetActiveAsync(ct);
        var regionOptions = activeRegions
            .Select(r => new StudentImportTemplateRegion(r.Name, r.Abbreviation, r.ModalityScope))
            .ToList();

        var bytes = writer.BuildStudentImportTemplate(regionOptions);
        return Result.Success(new StudentImportTemplateResponse("Plantilla_Alta_Alumnos.xlsx", Convert.ToBase64String(bytes)));
    }
}
