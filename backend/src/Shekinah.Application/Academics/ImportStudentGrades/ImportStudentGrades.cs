using Shekinah.Application.Abstractions;
using Shekinah.Application.Academics.ImportGrades;
using Shekinah.Domain.Academics;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics.ImportStudentGrades;

/// <summary>
/// "Calificar por alumno" → importar desde Excel (2026-10, pedido explícito del cliente: Bulk
/// Assignment por alumno). Mismo patrón fila-por-celda que <see cref="ImportGradesCommandHandler"/>
/// (nunca aborta el lote completo), pero acotado a un solo alumno — no necesita MATRICULA para
/// ubicarlo (ya viene en la ruta), así que cualquier columna que coincida con el nombre de una
/// materia en la que esté inscrito se procesa directo.
/// </summary>
[RequireRole(UserRole.Teacher, UserRole.Administrator)]
public sealed record ImportStudentGradesCommand(string StudentId, string PeriodId, string FileName, Stream Content)
    : ICommand<ImportStudentGradesResponse>;

public sealed record ImportStudentGradesResponse(int ImportedCells, IReadOnlyList<GradeImportCellError> Errors);

public sealed class ImportStudentGradesCommandHandler(
    ISpreadsheetReader spreadsheetReader, ICourseOfferingRepository offerings, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ImportStudentGradesCommand, ImportStudentGradesResponse>
{
    public async Task<Result<ImportStudentGradesResponse>> HandleAsync(ImportStudentGradesCommand command, CancellationToken ct)
    {
        if (!spreadsheetReader.CanRead(command.FileName))
        {
            return Result.Failure<ImportStudentGradesResponse>(
                Error.Validation("ImportStudentGrades.UnsupportedFormat", "Formato de archivo no soportado (use .xlsx o .csv)."));
        }

        var studentOfferings = await offerings.GetByStudentAndPeriodAsync(command.StudentId, command.PeriodId, ct);
        if (studentOfferings.Count == 0)
        {
            return Result.Failure<ImportStudentGradesResponse>(Error.Validation(
                "ImportStudentGrades.NoSubjects", "Este alumno no tiene materias asignadas en el periodo activo."));
        }

        var isAdministrator = currentUser.Role == UserRole.Administrator || currentUser.ExtraRoles.Contains(UserRole.Administrator);

        var rows = new List<SpreadsheetRow>();
        await foreach (var row in spreadsheetReader.ReadAsync(command.Content, command.FileName, ct))
        {
            rows.Add(row);
        }

        var errors = new List<GradeImportCellError>();
        var importedCells = 0;
        var touchedOfferings = new Dictionary<string, CourseOffering>();
        var gradedBy = currentUser.UserId ?? "system";

        foreach (var row in rows)
        {
            foreach (var offering in studentOfferings)
            {
                var header = offering.Subject.Name.ToUpperInvariant();
                if (!row.Values.TryGetValue(header, out var cellValue) || string.IsNullOrWhiteSpace(cellValue))
                {
                    continue; // celda vacía: el alumno sigue sin calificación en esa materia, no es error.
                }

                if (!isAdministrator && offering.Teacher?.Id != currentUser.UserId)
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, offering.Subject.Name, "NO_ES_DUEÑO",
                        "Solo el profesor asignado o un administrador puede calificar esta materia."));
                    continue;
                }

                if (!int.TryParse(cellValue.Trim(), out var gradeValue))
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, offering.Subject.Name, "CALIF_FUERA_DE_RANGO",
                        $"\"{cellValue}\" debe ser un número entero entre 0 y 10."));
                    continue;
                }

                var gradeResult = Grade.Create(gradeValue);
                if (gradeResult.IsFailure)
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, offering.Subject.Name, "CALIF_FUERA_DE_RANGO", gradeResult.Error.Message));
                    continue;
                }

                var recordResult = offering.RecordGrade(command.StudentId, gradeResult.Value, gradedBy, clock);
                if (recordResult.IsFailure)
                {
                    errors.Add(new GradeImportCellError(row.RowNumber, offering.Subject.Name, "CALIFICACION_RECHAZADA", recordResult.Error.Message));
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

        return Result.Success(new ImportStudentGradesResponse(importedCells, errors));
    }
}
