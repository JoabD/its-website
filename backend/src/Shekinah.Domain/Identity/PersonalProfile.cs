using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Identity;

/// <summary>
/// Perfil personal del usuario, EDITABLE (RN-23). Es un snapshot independiente del expediente de
/// admisión: se inicializa a partir de <c>ApplicantProfile</c> al aprobar, pero de ahí en adelante
/// vive su propio ciclo de vida (corrige hallazgo de auditoría #8).
/// </summary>
public sealed record PersonalProfile
{
    public PersonName FullName { get; }

    /// <summary>
    /// Opcional (ajuste de flujo real): en la práctica el administrador no siempre tiene el correo
    /// del alumno al capturarlo. Sin correo, el alumno queda de alta con matrícula, materias,
    /// calificaciones y pagos normales, pero SIN acceso al sistema (login es por correo — ver
    /// <c>LoginCommand</c> —, así que sin correo simplemente no hay forma de autenticar). El acceso
    /// se puede activar después capturando el correo.
    /// </summary>
    public Email? Email { get; }

    /// <summary>
    /// Opcional, mismo ajuste de flujo real que <see cref="Email"/>: el administrador tampoco
    /// siempre tiene el teléfono del alumno al capturarlo. Sin teléfono, el alumno igual queda de
    /// alta con normalidad — solo se queda sin la opción de "enviar por WhatsApp" (recibos, Kardex).
    /// </summary>
    public PhoneNumber? Phone { get; }

    public DateOnly BirthDate { get; }

    public string MaritalStatus { get; }

    public Address Address { get; }

    public ChurchInfo Church { get; }

    public EducationLevel Education { get; }

    public string TheologicalBackground { get; }

    public string StudyPurpose { get; }

    private PersonalProfile(
        PersonName fullName, Email? email, PhoneNumber? phone, DateOnly birthDate, string maritalStatus,
        Address address, ChurchInfo church, EducationLevel education, string theologicalBackground, string studyPurpose)
    {
        FullName = fullName;
        Email = email;
        Phone = phone;
        BirthDate = birthDate;
        MaritalStatus = maritalStatus;
        Address = address;
        Church = church;
        Education = education;
        TheologicalBackground = theologicalBackground;
        StudyPurpose = studyPurpose;
    }

    public static PersonalProfile FromApplicant(Admissions.ApplicantProfile applicant) => new(
        applicant.FullName, applicant.Email, applicant.Phone, applicant.BirthDate, applicant.MaritalStatus,
        applicant.Address, applicant.Church, applicant.Education, applicant.TheologicalBackground, applicant.StudyPurpose);

    public static Result<PersonalProfile> Create(
        PersonName fullName, Email? email, PhoneNumber? phone, DateOnly birthDate, string? maritalStatus,
        Address address, ChurchInfo church, EducationLevel education, string? theologicalBackground, string? studyPurpose)
    {
        if (birthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Result.Failure<PersonalProfile>(Error.Validation("PersonalProfile.InvalidBirthDate", "La fecha de nacimiento debe ser en el pasado."));
        }

        return Result.Success(new PersonalProfile(
            fullName, email, phone, birthDate, (maritalStatus ?? string.Empty).Trim(), address, church,
            education, (theologicalBackground ?? string.Empty).Trim(), (studyPurpose ?? string.Empty).Trim()));
    }

    /// <summary>Edad derivada; nunca almacenada (corrige hallazgo #7).</summary>
    public int AgeAsOf(DateOnly today)
    {
        var age = today.Year - BirthDate.Year;
        if (BirthDate > today.AddYears(-age)) age--;
        return age;
    }
}
