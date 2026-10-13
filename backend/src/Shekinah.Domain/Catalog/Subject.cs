using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Catalog;

public sealed record Syllabus
{
    public string? PdfUrl { get; }

    public DateTime? PublishedAtUtc { get; }

    private Syllabus(string? pdfUrl, DateTime? publishedAtUtc)
    {
        PdfUrl = pdfUrl;
        PublishedAtUtc = publishedAtUtc;
    }

    /// <summary>Sin PDF publicado ⇒ la UI muestra "Próximamente" (RN-20).</summary>
    public static Syllabus NotPublished => new(null, null);

    public static Syllabus Published(string pdfUrl, DateTime publishedAtUtc) => new(pdfUrl, publishedAtUtc);

    public bool IsPublished => PdfUrl is not null;
}

/// <summary>
/// Materia del plan de estudios. Cuatrimestral (1..6) o de diplomado (sin cuatrimestre) — RN-20,
/// invariante descrito en spec técnico §2.2.
/// </summary>
public sealed class Subject : AggregateRoot<string>
{
    private Subject() { }

    private Subject(string id, string code, int? legacyId, string name, ProgramType programType, TermNumber? termNumber, int displayOrder, StudyPlan plan)
        : base(id)
    {
        Code = code;
        LegacyId = legacyId;
        Name = name;
        ProgramType = programType;
        TermNumber = termNumber;
        DisplayOrder = displayOrder;
        Plan = plan;
        Syllabus = Syllabus.NotPublished;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public int? LegacyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ProgramType ProgramType { get; private set; }

    /// <summary>Nulo si <see cref="ProgramType"/> es <see cref="ProgramType.Diploma"/>.</summary>
    public TermNumber? TermNumber { get; private set; }

    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Plan de estudios al que pertenece esta materia: Cuatrimestral (Virtual) o Semestral
    /// (Presencial). Dos currículos distintos que coexisten — una materia solo pertenece a uno.
    /// Materias de diplomado son siempre Cuatrimestral (no existe diplomado semestral por ahora).
    /// </summary>
    public StudyPlan Plan { get; private set; }

    public Syllabus Syllabus { get; private set; } = Syllabus.NotPublished;

    public bool IsActive { get; private set; }

    public static Result<Subject> CreateQuarterly(string id, string code, string name, TermNumber termNumber, int displayOrder, int? legacyId = null, StudyPlan plan = StudyPlan.Quarterly)
    {
        if (termNumber.IsGraduated)
        {
            return Result.Failure<Subject>(Error.Validation("Subject.InvalidTerm", "Una materia cuatrimestral/semestral no puede tener cuatrimestre/semestre 'Egresado'."));
        }

        return ValidateAndCreate(id, code, legacyId, name, ProgramType.Quarterly, termNumber, displayOrder, plan);
    }

    public static Result<Subject> CreateDiploma(string id, string code, string name, int displayOrder, int? legacyId = null) =>
        ValidateAndCreate(id, code, legacyId, name, ProgramType.Diploma, null, displayOrder, StudyPlan.Quarterly);

    private static Result<Subject> ValidateAndCreate(string id, string code, int? legacyId, string name, ProgramType programType, TermNumber? termNumber, int displayOrder, StudyPlan plan)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Subject>(Error.Validation("Subject.Incomplete", "Código y nombre de la materia son requeridos."));
        }

        return Result.Success(new Subject(id, code.Trim(), legacyId, name.Trim(), programType, termNumber, displayOrder, plan));
    }

    public void PublishSyllabus(string pdfUrl, IClock clock) => Syllabus = Syllabus.Published(pdfUrl, clock.UtcNow);
}
