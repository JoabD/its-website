using Shekinah.Application.Abstractions;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.ImportGrades;

/// <summary>
/// Kardex → Calificaciones → "Importar desde Excel": mismo patrón que ImportStudents/ImportPayments
/// (reporta celda por celda SIN abortar el lote), pero a diferencia de esos, una fila aquí puede
/// tener varias celdas (una por materia del cuatrimestre/semestre) — por eso los errores se reportan
/// por celda (fila + materia), no solo por fila. Columnas esperadas: MATRICULA | ALUMNO | una
/// columna por cada materia del grupo (mismo orden que entrega GetGradeImportTemplateQuery). La
/// columna ALUMNO se ignora al importar — solo MATRICULA ubica al alumno, para que un nombre editado
/// a mano por error no cause inconsistencias.
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record ImportGradesCommand(string PeriodId, StudyPlan Plan, int TermNumber, string RegionId, string FileName, Stream Content)
    : ICommand<ImportGradesResponse>;

public sealed record GradeImportCellError(int RowNumber, string SubjectName, string Code, string Message);

public sealed record ImportGradesResponse(int TotalRows, int TotalCells, int ImportedCells, IReadOnlyList<GradeImportCellError> Errors);

public sealed class ImportGradesCommandHandler(
    ISpreadsheetReader spreadsheetReader, Domain.Catalog.ISubjectRepository subjects,
    ICourseOfferingRepository offerings, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ImportGradesCommand, ImportGradesResponse>
{
    public async Task<Result<ImportGradesResponse>> HandleAsync(ImportGradesCommand command, CancellationToken ct)
    {
        if (!spreadsheetReader.CanRead(command.FileName))
        {
            return Result.Failure<ImportGradesResponse>(
                Error.Validation("ImportGrades.UnsupportedFormat", "Formato de archivo no soportado (use .xlsx o .csv)."));
        }

        var termResult = TermNumber.Create(command.TermNumber);
        if (termResult.IsFailure)
        {
            return Result.Failure<ImportGradesResponse>(termResult.Error);
        }

        var termSubjects = await subjects.GetByTermAndPlanAsync(termResult.Value, command.Plan, ct);
        if (termSubjects.Count == 0)
        {
            return Result.Failure<ImportGradesResponse>(Error.Validation(
                "ImportGrades.NoSubjects", "No hay materias dadas de alta para este plan y cuatrimestre/semestre."));
        }

        var periodOfferings = await offerings.GetByPeriodAsync(command.PeriodId, ct);
        var subjectIds = termSubjects.Select(s => s.Id).ToHashSet();
        var relevantOfferings = periodOfferings
            .Where(o => subjectIds.Contains(o.Subject.Id) && o.Region.Id == command.RegionId)
            .ToList();

        // Mapa por nombre de materia en MAYÚSCULAS (así se generan los encabezados en la plantilla) →
        // oferta real de ese grupo. Una materia del cuatrimestre/semestre sin oferta creada (nadie
        // inscrito todavía) simplemente no tiene a quién calificar — su columna, si viene en el
        // archivo, se ignora fila por fila con MATRICULA_NO_ENCONTRADA (no hay ofertas para validar
        // contra, así que no hay alumno que coincida).
        var offeringsBySubjectName = relevantOfferings
            .GroupBy(o => o.Subject.Name.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<SpreadsheetRow>();
        await foreach (var row in spreadsheetReader.ReadAsync(command.Content, command.FileName, ct))
        {
            rows.Add(row);
        }

        var errors = new List<GradeImportCellError>();
        var totalCells = 0;
        var importedCells = 0;
        var touchedOfferings = new Dictionary<string, CourseOffering>();
        var gradedBy = currentUser.UserId ?? "system";

        foreach (var row in rows)
        {
            var matriculaRaw = row.Values.GetValueOrDefault("MATRICULA", "").Trim();
            if (!int.TryParse(matriculaRaw, out var matricula))
            {
                errors.Add(new GradeImportCellError(row.RowNumber, "-", "MATRICULA_INVALIDA", $"\"{matriculaRaw}\" no es una matrícula válida."));
                continue;
            }

            foreach (var subjectName in termSubjects.Select(s => s.Name))
            {
                var header = subjectName.ToUpperInvariant();
                if (!row.Values.TryGetValue(header, out var cellValue) || string.IsNullOrWhiteSpace(cellValue))
                {
                    continue; // celda vacía: el alumno simplemente sigue sin calificación, no es error.
                }

                totalCells++;

                if (!offeringsBySubjectName.TryGetValue(header, out var offering))
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, subjectName, "MATERIA_SIN_GRUPO", $"\"{subjectName}\" no tiene un grupo creado en este periodo/región todavía."));
                    continue;
                }

                if (!int.TryParse(cellValue.Trim(), out var gradeValue))
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, subjectName, "CALIF_FUERA_DE_RANGO", $"\"{cellValue}\" debe ser un número entero entre 0 y 10."));
                    continue;
                }

                var gradeResult = Grade.Create(gradeValue);
                if (gradeResult.IsFailure)
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, subjectName, "CALIF_FUERA_DE_RANGO", gradeResult.Error.Message));
                    continue;
                }

                var enrollment = offering.Enrollments.FirstOrDefault(e => e.EnrollmentNumber == matricula);
                if (enrollment is null)
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, subjectName, "MATRICULA_NO_ENCONTRADA", $"La matrícula \"{matricula}\" no está inscrita en \"{subjectName}\" en este grupo."));
                    continue;
                }

                var recordResult = offering.RecordGrade(enrollment.StudentId, gradeResult.Value, gradedBy, clock);
                if (recordResult.IsFailure)
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, subjectName, "CALIFICACION_RECHAZADA", recordResult.Error.Message));
                    continue;
                }

                touchedOfferings.TryAdd(offering.Id, offering);
                importedCells++;
            }
        }

        foreach (var offering in touchedOfferings.Values)
        {
            await offerings.UpdateAsync(offering, ct);
        }

        return Result.Success(new ImportGradesResponse(rows.Count, totalCells, importedCells, errors));
    }
}
