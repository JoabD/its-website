using Shekinah.Domain.Academics;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Application.Academics;

/// <summary>
/// "Promover alumnos" (2026-10, segunda vuelta de feedback del cliente: "una sola acción [que]
/// promuevan al siguiente semestre o cuatrimestre, y los que tengan... una materia reprobada o
/// simplemente no tiene calificación en alguna o todas las materias solo lo ignore"). Decide si UN
/// alumno puede subir de cuatrimestre/semestre: todas las materias de su currículo actual deben
/// tener, en el periodo activo, una calificación aprobatoria (<see cref="Grade.IsPassing"/>) — una
/// materia sin oferta, sin inscripción, sin calificar, o reprobada, basta para que NO sea
/// elegible. Compartida entre <c>PromoteStudentCommand</c> (un alumno, desde "Calificar por
/// alumno") y <c>PromoteStudentsCommand</c> (todo un plan+término a la vez, desde "Académico") para
/// no duplicar el criterio.
/// </summary>
public static class PromotionEligibilityService
{
    public static async Task<(bool Eligible, string? Reason)> EvaluateAsync(
        User student, string periodId, ISubjectRepository subjects, ICourseOfferingRepository offerings, CancellationToken ct)
    {
        if (student.Academic is null || student.Academic.IsGraduated)
        {
            return (false, "Este alumno no tiene cuatrimestre/semestre activo.");
        }

        var curriculum = await subjects.GetByTermAndPlanAsync(student.Academic.CurrentTerm, student.Academic.Plan, ct);
        if (curriculum.Count == 0)
        {
            return (false, "Este alumno no tiene materias de currículo para su cuatrimestre/semestre actual.");
        }

        var studentOfferings = await offerings.GetByStudentAndPeriodAsync(student.Id, periodId, ct);
        var gradeBySubjectId = new Dictionary<string, int?>();
        foreach (var offering in studentOfferings)
        {
            var enrollment = offering.Enrollments.FirstOrDefault(e => e.StudentId == student.Id);
            if (enrollment is not null)
            {
                gradeBySubjectId[offering.Subject.Id] = enrollment.Grade?.Value;
            }
        }

        foreach (var subject in curriculum)
        {
            if (!gradeBySubjectId.TryGetValue(subject.Id, out var grade) || grade is null)
            {
                return (false, $"Falta calificación en \"{subject.Name}\".");
            }

            if (grade.Value < Grade.PassingValue)
            {
                return (false, $"\"{subject.Name}\" está reprobada ({grade.Value}).");
            }
        }

        return (true, null);
    }
}
