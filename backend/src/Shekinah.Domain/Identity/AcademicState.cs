using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Identity;

/// <summary>Estado académico del alumno. Solo tiene sentido cuando <see cref="User.Role"/> es Student.</summary>
public sealed record AcademicState
{
    public TermNumber CurrentTerm { get; }

    public bool IsGraduated => CurrentTerm.IsGraduated;

    public DateTime EnrolledAtUtc { get; }

    public DateTime? GraduatedAtUtc { get; }

    private AcademicState(TermNumber currentTerm, DateTime enrolledAtUtc, DateTime? graduatedAtUtc)
    {
        CurrentTerm = currentTerm;
        EnrolledAtUtc = enrolledAtUtc;
        GraduatedAtUtc = graduatedAtUtc;
    }

    public static AcademicState Start(IClock clock) => new(TermNumber.First, clock.UtcNow, null);

    public static AcademicState Rehydrate(TermNumber currentTerm, DateTime enrolledAtUtc, DateTime? graduatedAtUtc) =>
        new(currentTerm, enrolledAtUtc, graduatedAtUtc);

    /// <summary>RN-14: promoción de cuatrimestre al cerrar un periodo.</summary>
    public AcademicState Promote(IClock clock)
    {
        var next = CurrentTerm.Promote();
        return new AcademicState(next, EnrolledAtUtc, next.IsGraduated ? clock.UtcNow : null);
    }
}
