using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Catalog;

public interface ISubjectRepository
{
    Task<Subject?> GetByIdAsync(string id, CancellationToken ct);

    Task<Subject?> GetByCodeAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<Subject>> GetCurriculumAsync(CancellationToken ct);

    Task<IReadOnlyList<Subject>> GetByTermAsync(TermNumber termNumber, CancellationToken ct);

    /// <summary>Materias de un cuatrimestre/semestre puntual DENTRO de un plan de estudios
    /// específico (Cuatrimestral o Semestral) — los dos currículos coexisten y no se mezclan.</summary>
    Task<IReadOnlyList<Subject>> GetByTermAndPlanAsync(TermNumber termNumber, StudyPlan plan, CancellationToken ct);

    /// <summary>Todo el currículo activo de un plan de estudios, ordenado por cuatrimestre/semestre.</summary>
    Task<IReadOnlyList<Subject>> GetByPlanAsync(StudyPlan plan, CancellationToken ct);

    Task AddAsync(Subject subject, CancellationToken ct);

    Task UpdateAsync(Subject subject, CancellationToken ct);

    Task DeleteAsync(string id, CancellationToken ct);
}
