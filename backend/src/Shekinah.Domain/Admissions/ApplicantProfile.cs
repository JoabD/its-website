using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Admissions;

/// <summary>
/// Snapshot inmutable de los datos personales/eclesiásticos/de formación capturados en la
/// solicitud. Se COPIA a <c>User.Profile</c> al aprobar (RN-05); editar el perfil del alumno
/// después NO muta este snapshot histórico (RN-23, hallazgo de auditoría #8).
/// </summary>
public sealed record ApplicantProfile
{
    public PersonName FullName { get; }

    public DateOnly BirthDate { get; }

    public string MaritalStatus { get; }

    public Email Email { get; }

    public PhoneNumber Phone { get; }

    public Address Address { get; }

    public ChurchInfo Church { get; }

    public EducationLevel Education { get; }

    public string TheologicalBackground { get; }

    public string StudyPurpose { get; }

    private ApplicantProfile(
        PersonName fullName, DateOnly birthDate, string maritalStatus, Email email, PhoneNumber phone,
        Address address, ChurchInfo church, EducationLevel education, string theologicalBackground, string studyPurpose)
    {
        FullName = fullName;
        BirthDate = birthDate;
        MaritalStatus = maritalStatus;
        Email = email;
        Phone = phone;
        Address = address;
        Church = church;
        Education = education;
        TheologicalBackground = theologicalBackground;
        StudyPurpose = studyPurpose;
    }

    public static Result<ApplicantProfile> Create(
        PersonName fullName, DateOnly birthDate, string? maritalStatus, Email email, PhoneNumber phone,
        Address address, ChurchInfo church, EducationLevel education, string? theologicalBackground, string? studyPurpose)
    {
        if (birthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Result.Failure<ApplicantProfile>(Error.Validation("ApplicantProfile.InvalidBirthDate", "La fecha de nacimiento debe ser en el pasado."));
        }

        if (string.IsNullOrWhiteSpace(studyPurpose))
        {
            return Result.Failure<ApplicantProfile>(Error.Validation("ApplicantProfile.MissingPurpose", "El propósito de estudio es requerido."));
        }

        return Result.Success(new ApplicantProfile(
            fullName, birthDate, (maritalStatus ?? string.Empty).Trim(), email, phone, address, church,
            education, (theologicalBackground ?? string.Empty).Trim(), studyPurpose.Trim()));
    }

    /// <summary>
    /// Edad DERIVADA de la fecha de nacimiento (corrige el hallazgo #7: el legado la capturaba a
    /// mano por separado y se desincronizaba).
    /// </summary>
    public int AgeAsOf(DateOnly today)
    {
        var age = today.Year - BirthDate.Year;
        if (BirthDate > today.AddYears(-age)) age--;
        return age;
    }
}
