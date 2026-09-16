using Shekinah.Domain.Common;
using Shekinah.Domain.Identity;

namespace Shekinah.Domain.Academics.Policies;

/// <summary>
/// RN-14: al abrir un periodo nuevo, en una sola transacción se (a) promueve el cuatrimestre de
/// los alumnos activos con inscripciones en el periodo que se cierra, (b) se cierra el periodo
/// activo y (c) se crea y activa el nuevo. La orquestación transaccional vive en el handler de
/// Application (OpenPeriodCommandHandler, vía ITransactionalCommand); esta política pura solo
/// decide QUIÉN se promueve y a qué término, sin tocar infraestructura.
/// </summary>
public static class TermPromotionPolicy
{
    /// <summary>
    /// Aplica la promoción a la lista de alumnos activos entregada por el handler (ya filtrada por
    /// "con inscripciones en el periodo que se cierra"). Devuelve los resultados individuales para
    /// que el handler decida cómo abortar la transacción si alguno falla.
    /// </summary>
    public static IReadOnlyList<Result> PromoteAll(IEnumerable<User> activeStudentsInClosingPeriod, Common.IClock clock) =>
        activeStudentsInClosingPeriod.Select(student => student.PromoteTerm(clock)).ToList();
}
