using System.Globalization;
using Shekinah.Application.Abstractions;
using Shekinah.Application.Identity.CreateUser;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Identity.ImportStudents;

/// <summary>
/// Alta manual de alumnos por Excel (plan de control escolar → Alumnos → "Agregar alumno" →
/// Importar Excel): mismo patrón que ImportPayments — reporta fila a fila SIN abortar el lote
/// (una fila mala no tumba a las demás). Columnas esperadas, en este orden exacto (ver plantilla
/// oficial "Plantilla_Alta_Alumnos.xlsx"):
///   NOMBRE_COMPLETO | CORREO | TELEFONO | FECHA_NACIMIENTO (dd/mm/aaaa) | REGION (nombre o
///   abreviatura de una región activa) | MODALIDAD (Presencial|Virtual|Diplomado) |
///   PLAN (Cuatrimestral|Semestral) | CUATRIMESTRE_O_SEMESTRE (1-6, el punto en el que entra).
/// </summary>
[RequireRole(UserRole.Administrator)]
public sealed record ImportStudentsCommand(string FileName, Stream Content) : ICommand<ImportStudentsResponse>;

public sealed record ImportStudentsResponse(string BatchId, int TotalRows, int ImportedRows, IReadOnlyList<StudentImportRowError> Errors);

public sealed class ImportStudentsCommandHandler(
    ISpreadsheetReader spreadsheetReader, IUserRepository users, Domain.Catalog.IRegionRepository regions,
    IEnrollmentNumberGenerator enrollmentNumbers, IMatriculaGenerator matriculaGenerator,
    IPasswordHasher passwordHasher, IEmailSender emailSender, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ImportStudentsCommand, ImportStudentsResponse>
{
    private static readonly string[] BirthDateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];

    public async Task<Result<ImportStudentsResponse>> HandleAsync(ImportStudentsCommand command, CancellationToken ct)
    {
        if (!spreadsheetReader.CanRead(command.FileName))
        {
            return Result.Failure<ImportStudentsResponse>(
                Error.Validation("ImportStudents.UnsupportedFormat", "Formato de archivo no soportado (use .xlsx o .csv)."));
        }

        var rows = new List<SpreadsheetRow>();
        await foreach (var row in spreadsheetReader.ReadAsync(command.Content, command.FileName, ct))
        {
            rows.Add(row);
        }

        var batch = await users.RegisterStudentImportBatchAsync(command.FileName, currentUser.UserId ?? "system", rows.Count, ct);
        var errors = new List<StudentImportRowError>();
        var imported = 0;

        var activeRegions = await regions.GetActiveAsync(ct);

        foreach (var row in rows)
        {
            var raw = new[]
            {
                row.Values.GetValueOrDefault("NOMBRE_COMPLETO", ""),
                row.Values.GetValueOrDefault("CORREO", ""),
                row.Values.GetValueOrDefault("TELEFONO", ""),
                row.Values.GetValueOrDefault("FECHA_NACIMIENTO", ""),
                row.Values.GetValueOrDefault("REGION", ""),
                row.Values.GetValueOrDefault("MODALIDAD", ""),
                row.Values.GetValueOrDefault("PLAN", ""),
                row.Values.GetValueOrDefault("CUATRIMESTRE_O_SEMESTRE", ""),
            };

            var nameResult = PersonName.Create(row.Values.GetValueOrDefault("NOMBRE_COMPLETO"));
            if (nameResult.IsFailure)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_NOMBRE", nameResult.Error.Message, raw));
                continue;
            }

            var emailResult = Email.Create(row.Values.GetValueOrDefault("CORREO"));
            if (emailResult.IsFailure)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_CORREO", emailResult.Error.Message, raw));
                continue;
            }

            var phoneResult = PhoneNumber.Create(row.Values.GetValueOrDefault("TELEFONO"));
            if (phoneResult.IsFailure)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_TELEFONO", phoneResult.Error.Message, raw));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    row.Values.GetValueOrDefault("FECHA_NACIMIENTO"), BirthDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_FECHA_NACIMIENTO", "FECHA_NACIMIENTO debe tener formato dd/mm/aaaa.", raw));
                continue;
            }

            var regionRaw = row.Values.GetValueOrDefault("REGION", "").Trim();
            var regionEntity = activeRegions.FirstOrDefault(r =>
                string.Equals(r.Name, regionRaw, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Abbreviation, regionRaw, StringComparison.OrdinalIgnoreCase));
            if (regionEntity is null)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_REGION", $"No existe una región activa llamada \"{regionRaw}\".", raw));
                continue;
            }

            if (!TryParseModality(row.Values.GetValueOrDefault("MODALIDAD"), out var modality))
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_MODALIDAD", "MODALIDAD debe ser Presencial, Virtual o Diplomado.", raw));
                continue;
            }

            if (!TryParsePlan(row.Values.GetValueOrDefault("PLAN"), out var plan))
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_PLAN", "PLAN debe ser Cuatrimestral o Semestral.", raw));
                continue;
            }

            if (!int.TryParse(row.Values.GetValueOrDefault("CUATRIMESTRE_O_SEMESTRE"), out var termRaw))
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_CUATRIMESTRE", "CUATRIMESTRE_O_SEMESTRE debe ser numérico (1-6).", raw));
                continue;
            }

            var termResult = TermNumber.Create(termRaw);
            if (termResult.IsFailure)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_CUATRIMESTRE", termResult.Error.Message, raw));
                continue;
            }

            var existing = await users.GetByEmailAsync(emailResult.Value.Value, ct);
            if (existing is not null)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "EMAIL_IN_USE", $"Ya existe un usuario con el correo {emailResult.Value.Value}.", raw));
                continue;
            }

            var address = Address.Create("N/D", "N/D", "N/D", "N/D").Value;
            var church = ChurchInfo.Create("N/D", address, "N/D", "N/D", MinistryRole.None).Value;
            var education = EducationLevel.Create(SchoolingLevel.Other, "N/D").Value;
            var profileResult = PersonalProfile.Create(
                nameResult.Value, emailResult.Value, phoneResult.Value, birthDate, null, address, church, education, null, null);
            if (profileResult.IsFailure)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "INVALID_PROFILE", profileResult.Error.Message, raw));
                continue;
            }

            var region = new RegionRef(regionEntity.Id, regionEntity.LegacyCode, regionEntity.Name);
            var enrollmentNumber = await enrollmentNumbers.NextAsync(ct);
            var matricula = await matriculaGenerator.NextAsync(regionEntity.Abbreviation, ct);
            var temporaryPassword = TemporaryPasswordGenerator.Generate();

            var userResult = User.CreateStudentManually(
                EntityId.NewId(), enrollmentNumber, matricula, profileResult.Value, modality, region,
                plan, termResult.Value, passwordHasher.Hash(temporaryPassword), clock);
            if (userResult.IsFailure)
            {
                errors.Add(new StudentImportRowError(row.RowNumber, "CREATE_FAILED", userResult.Error.Message, raw));
                continue;
            }

            await users.AddAsync(userResult.Value, ct);

            await emailSender.SendAsync(
                emailResult.Value.Value,
                "Tus credenciales de acceso — Instituto Teológico Shekinah",
                $"<p>Matrícula: {matricula}</p><p>Contraseña temporal: {temporaryPassword}</p><p>Deberás cambiarla en tu primer inicio de sesión.</p>",
                ct);

            imported++;
        }

        await users.CompleteStudentImportBatchAsync(batch.Id, imported, errors, ct);
        return Result.Success(new ImportStudentsResponse(batch.Id, rows.Count, imported, errors));
    }

    private static bool TryParseModality(string? raw, out Modality modality)
    {
        switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "presencial": modality = Modality.Onsite; return true;
            case "virtual": modality = Modality.Online; return true;
            case "diplomado": modality = Modality.Diploma; return true;
            default: modality = default; return false;
        }
    }

    private static bool TryParsePlan(string? raw, out StudyPlan plan)
    {
        switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "cuatrimestral": plan = StudyPlan.Quarterly; return true;
            case "semestral": plan = StudyPlan.Semester; return true;
            default: plan = default; return false;
        }
    }
}
