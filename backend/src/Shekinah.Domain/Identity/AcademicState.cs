using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Identity;

/// <summary>Estado académico del alumno. Solo tiene sentido cuando <see cref="User.Role"/> es Student.</summary>
public sealed record AcademicState
{
    public TermNumber CurrentTerm { get; }

    /// <summary>Cuatrimestral (RN de producto: SIEMPRE para solicitudes públicas aprobadas) o
    /// Semestral (solo alta manual/Excel). Puramente informativo por ahora — ver <see cref="StudyPlan"/>.</summary>
    public StudyPlan Plan { get; }

    public bool IsGraduated => CurrentTerm.IsGraduated;

    public DateTime EnrolledAtUtc { get; }

    public DateTime? GraduatedAtUtc { get; }

    private AcademicState(StudyPlan plan, TermNumber currentTerm, DateTime enrolledAtUtc, DateTime? graduatedAtUtc)
    {
        Plan = plan;
        CurrentTerm = currentTerm;
        EnrolledAtUtc = enrolledAtUtc;
        GraduatedAtUtc = graduatedAtUtc;
    }

    /// <summary>RN-05: toda solicitud aprobada arranca Cuatrimestral, cuatrimestre 1 — nunca negociable
    /// desde este punto de entrada (para eso está <see cref="StartManual"/>).</summary>
    public static AcademicState Start(IClock clock) => new(StudyPlan.Quarterly, TermNumber.First, clock.UtcNow, null);

    /// <summary>Alta manual/Excel (plan de control escolar): el administrador elige plan y en qué
    /// cuatrimestre/semestre entra el alumno — típicamente alumnos que ya venían cursando fuera del
    /// sistema. No se permite dar de alta a alguien ya "Egresado" por esta vía.</summary>
    public static Result<AcademicState> StartManual(StudyPlan plan, TermNumber currentTerm, IClock clock)
    {
        if (currentTerm.IsGraduated)
        {
            return Result.Failure<AcademicState>(Error.Validation(
                "AcademicState.CannotStartGraduated", "No se puede dar de alta a un alumno ya egresado."));
        }

        return Result.Success(new AcademicState(plan, currentTerm, clock.UtcNow, null));
    }

    public static AcademicState Rehydrate(StudyPlan plan, TermNumber currentTerm, DateTime enrolledAtUtc, DateTime? graduatedAtUtc) =>
        new(plan, currentTerm, enrolledAtUtc, graduatedAtUtc);

    /// <summary>RN-14: promoción de cuatrimestre al cerrar un periodo. El plan nunca cambia por esto.</summary>
    public AcademicState Promote(IClock clock)
    {
        var next = CurrentTerm.Promote();
        return new AcademicState(Plan, next, EnrolledAtUtc, next.IsGraduated ? clock.UtcNow : null);
    }
}
