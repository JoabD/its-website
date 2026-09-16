using Shekinah.Domain.Admissions;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;
using Shekinah.Migrator.Legacy;

namespace Shekinah.Migrator.Mapping;

/// <summary>Mapeo PREREGISTRO → admissionApplications (ESPECIFICACION-TECNICA.md §5.10).</summary>
public static class AdmissionApplicationMapper
{
    public static Result<AdmissionApplication> Map(LegacyPreregistro legacy, Region region, string newId, string folio, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(legacy.Correo))
        {
            return Result.Failure<AdmissionApplication>(Error.Validation("Migrator.MissingEmail", $"PREREGISTRO.ID_ENCUESTA={legacy.IdEncuesta} no tiene CORREO."));
        }

        var educationLevel =
            legacy.Primaria ? SchoolingLevel.Primary :
            legacy.Secundaria ? SchoolingLevel.Secondary :
            legacy.Bachillerato ? SchoolingLevel.HighSchool : SchoolingLevel.Other;

        var education = EducationLevel.Create(educationLevel, legacy.OtraEscolaridad);
        var address = Address.Create(legacy.Domicilio, legacy.Colonia, legacy.Localidad, legacy.Municipio, legacy.Estado);
        var churchAddress = Address.Create(legacy.DomicilioIglesia, legacy.ColoniaIglesia, legacy.LocalidadIglesia, legacy.MunicipioIglesia);
        var ministryRole = MinistryRole.Create(legacy.Cargo is > 0, legacy.CargoNombre);

        if (education.IsFailure) return Result.Failure<AdmissionApplication>(education.Error);
        if (address.IsFailure) return Result.Failure<AdmissionApplication>(address.Error);
        if (churchAddress.IsFailure) return Result.Failure<AdmissionApplication>(churchAddress.Error);
        if (ministryRole.IsFailure) return Result.Failure<AdmissionApplication>(ministryRole.Error);

        var church = ChurchInfo.Create(legacy.Iglesia ?? "N/D", churchAddress.Value, legacy.Pastor ?? "N/D", legacy.TiempoCongregarse ?? "N/D", ministryRole.Value);
        if (church.IsFailure) return Result.Failure<AdmissionApplication>(church.Error);

        var name = PersonName.Create(legacy.NombreCompleto);
        var email = Email.Create(legacy.Correo);
        var phone = PhoneNumber.Create(legacy.Telefono ?? "0000000000");
        if (name.IsFailure) return Result.Failure<AdmissionApplication>(name.Error);
        if (email.IsFailure) return Result.Failure<AdmissionApplication>(email.Error);
        if (phone.IsFailure) return Result.Failure<AdmissionApplication>(phone.Error);

        // EDAD se descarta (se deriva de FECHA_NACIMIENTO) — spec técnico §5.10.
        var applicant = ApplicantProfile.Create(
            name.Value, DateOnly.FromDateTime(legacy.FechaNacimiento ?? new DateTime(1900, 1, 1)), legacy.EstadoCivil,
            email.Value, phone.Value, address.Value, church.Value, education.Value, legacy.FormacionTeologica, legacy.Proposito ?? "N/D");
        if (applicant.IsFailure) return Result.Failure<AdmissionApplication>(applicant.Error);

        var modality = legacy.Diplomado ? Modality.Diploma : legacy.Virtual1 ? Modality.Online : Modality.Onsite;
        var modalityChoice = ModalityChoice.Create(modality, region, legacy.MotivoVirtual);
        if (modalityChoice.IsFailure) return Result.Failure<AdmissionApplication>(modalityChoice.Error);

        var application = AdmissionApplication.Submit(newId, folio, applicant.Value, modalityChoice.Value, [], clock);
        if (application.IsFailure) return application;

        var status = legacy.Estatus switch { 1 => ApplicationStatus.Approved, 2 => ApplicationStatus.Rejected, _ => ApplicationStatus.Pending };
        if (status == ApplicationStatus.Pending)
        {
            return application;
        }

        // Rehidratar directamente en el estado decidido (el migrador no reproduce el flujo de aprobación/rechazo).
        var rehydrated = AdmissionApplication.Rehydrate(
            newId, folio, legacy.IdEncuesta, applicant.Value, modalityChoice.Value, status, legacy.FechaRegistro,
            new ApplicationDecision(legacy.FechaRegistro, "legacy-migration", "Migración de datos", "Estado heredado del sistema legado."), null);

        return Result.Success(rehydrated);
    }
}
