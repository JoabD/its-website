using Shekinah.Domain.Academics;
using Shekinah.Domain.Academics.Policies;
using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;

namespace Shekinah.Application.Academics;

/// <summary>
/// Matricula, en una oferta puntual, a los candidatos elegibles según <see cref="AutoEnrollmentPolicy"/>
/// (RN-15). Extraído del cuerpo de <c>AutoEnrollCommandHandler</c> (2026-10, "promover alumnos") para
/// que <c>PromoteStudentsCommandHandler</c> pueda reusar exactamente la misma lógica de matriculación
/// automática sin duplicarla — ambos casos terminan necesitando "dado un conjunto de alumnos
/// candidatos y una oferta, matricula a los que apliquen", solo que llegan a ese conjunto de
/// candidatos de forma distinta (por región del periodo completo vs. por los recién promovidos).
/// </summary>
public static class CourseEnrollmentService
{
    public static Result<int> EnrollEligible(CourseOffering offering, IReadOnlyList<User> candidateStudents, IClock clock)
    {
        var eligible = AutoEnrollmentPolicy.SelectEligibleStudents(candidateStudents, offering);
        var created = 0;

        foreach (var student in eligible)
        {
            var before = offering.EnrollmentCount;
            var result = offering.Enroll(student.Id, student.EnrollmentNumber.Value, student.Profile.FullName.FullName, student.Academic!.CurrentTerm, clock);
            if (result.IsFailure)
            {
                return Result.Failure<int>(result.Error);
            }

            if (offering.EnrollmentCount > before)
            {
                created++;
            }
        }

        return Result.Success(created);
    }
}
