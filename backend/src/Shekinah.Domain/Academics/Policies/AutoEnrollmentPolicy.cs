using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Academics.Policies;

/// <summary>
/// RN-15 — reconstruida del contrato observable de <c>insertar_materias_alumnos()</c> sin acceso
/// al cuerpo del procedimiento (PROMPT-MAESTRO.md §2.4 y §11: "es la especificación vigente, no un
/// supuesto a confirmar"). Matricula, en cada oferta del periodo activo, a los alumnos activos,
/// aprobados, cuyo <c>currentTerm</c> coincide con el cuatrimestre de la materia y cuya región
/// coincide con la de la oferta. Es idempotente (la idempotencia real la garantiza
/// <see cref="CourseOffering.Enroll"/>, aquí solo se decide el universo elegible).
/// </summary>
public static class AutoEnrollmentPolicy
{
    public static bool IsEligible(User student, CourseOffering offering)
    {
        if (student.Role != UserRole.Student || student.Status != UserStatus.Active || student.Academic is null)
        {
            return false;
        }

        if (student.Academic.IsGraduated)
        {
            return false;
        }

        var subjectTerm = offering.Subject.TermNumber;
        if (subjectTerm is null)
        {
            // Materia de diplomado: el diplomado se matricula por región de diplomado, no por término.
            return student.Region?.Id == offering.Region.Id;
        }

        return student.Academic.CurrentTerm.Value == subjectTerm.Value && student.Region?.Id == offering.Region.Id;
    }

    public static IReadOnlyList<User> SelectEligibleStudents(IEnumerable<User> candidateStudents, CourseOffering offering) =>
        candidateStudents.Where(s => IsEligible(s, offering)).ToList();
}
