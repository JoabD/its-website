using Shekinah.Domain.Academics.Events;
using Shekinah.Domain.Common;
using Shekinah.Domain.SharedKernel;

namespace Shekinah.Domain.Academics;

public sealed class Enrollment
{
    private Enrollment() { }

    private Enrollment(string studentId, int enrollmentNumber, string studentName, TermNumber termAtEnrollment, IClock clock)
    {
        StudentId = studentId;
        EnrollmentNumber = enrollmentNumber;
        StudentName = studentName;
        TermAtEnrollment = termAtEnrollment;
        Status = EnrollmentStatus.Active;
        EnrolledAtUtc = clock.UtcNow;
    }

    public string StudentId { get; private set; } = string.Empty;

    public int EnrollmentNumber { get; private set; }

    /// <summary>Snapshot de visualización (spec técnico §5.1); la fuente de verdad es <c>users</c>.</summary>
    public string StudentName { get; private set; } = string.Empty;

    public TermNumber TermAtEnrollment { get; private set; } = null!;

    public Grade? Grade { get; private set; }

    public DateTime? GradedAtUtc { get; private set; }

    public string? GradedByUserId { get; private set; }

    public DateTime EnrolledAtUtc { get; private set; }

    public EnrollmentStatus Status { get; private set; }

    internal static Enrollment Create(string studentId, int enrollmentNumber, string studentName, TermNumber termAtEnrollment, IClock clock) =>
        new(studentId, enrollmentNumber, studentName, termAtEnrollment, clock);

    /// <summary>Reconstrucción desde Infrastructure (mapeo Mongo) o desde el migrador.</summary>
    public static Enrollment Rehydrate(
        string studentId, int enrollmentNumber, string studentName, TermNumber termAtEnrollment, Grade? grade,
        DateTime? gradedAtUtc, string? gradedByUserId, DateTime enrolledAtUtc, EnrollmentStatus status) => new()
    {
        StudentId = studentId,
        EnrollmentNumber = enrollmentNumber,
        StudentName = studentName,
        TermAtEnrollment = termAtEnrollment,
        Grade = grade,
        GradedAtUtc = gradedAtUtc,
        GradedByUserId = gradedByUserId,
        EnrolledAtUtc = enrolledAtUtc,
        Status = status,
    };

    internal Result RecordGrade(Grade grade, string gradedByUserId, IClock clock)
    {
        if (Status != EnrollmentStatus.Active)
        {
            return Result.Failure(Error.Conflict("Enrollment.Dropped", "No se puede calificar una inscripción dada de baja."));
        }

        Grade = grade;
        GradedAtUtc = clock.UtcNow;
        GradedByUserId = gradedByUserId;
        return Result.Success();
    }
}

public sealed record TeacherRef(string Id, int EnrollmentNumber, string Name);

public sealed record PeriodRef(string Id, string Code);

public sealed record SubjectRef(string Id, string Name, int? TermNumber);

/// <summary>
/// Agregado raíz Academics. Sustituye <c>MATERIAS_ASIGNADAS</c> y elimina su doble semántica
/// (oferta docente vs. inscripción de alumno en la misma fila, PROMPT-MAESTRO.md §2.4). Único por
/// (periodo, materia, región) — RN-22. Embebe sus <see cref="Enrollments"/> (cota conocida, spec
/// técnico §5.2: si se acerca a 200, se migra a colección referenciada).
/// </summary>
public sealed class CourseOffering : AggregateRoot<string>
{
    public const int EmbeddedEnrollmentSoftLimit = 150;
    public const int EmbeddedEnrollmentHardLimit = 200;

    private readonly List<Enrollment> _enrollments = [];

    private CourseOffering() { }

    private CourseOffering(string id, PeriodRef period, SubjectRef subject, RegionRefAcademics region, TeacherRef teacher)
        : base(id)
    {
        Period = period;
        Subject = subject;
        Region = region;
        Teacher = teacher;
    }

    public PeriodRef Period { get; private set; } = null!;

    public SubjectRef Subject { get; private set; } = null!;

    public RegionRefAcademics Region { get; private set; } = null!;

    public TeacherRef Teacher { get; private set; } = null!;

    public IReadOnlyList<Enrollment> Enrollments => _enrollments.AsReadOnly();

    public int EnrollmentCount => _enrollments.Count;

    public static Result<CourseOffering> Create(string id, PeriodRef period, SubjectRef subject, RegionRefAcademics region, TeacherRef teacher) =>
        Result.Success(new CourseOffering(id, period, subject, region, teacher));

    public static CourseOffering Rehydrate(string id, PeriodRef period, SubjectRef subject, RegionRefAcademics region, TeacherRef teacher, IEnumerable<Enrollment> enrollments)
    {
        var offering = new CourseOffering(id, period, subject, region, teacher);
        offering._enrollments.AddRange(enrollments);
        return offering;
    }

    /// <summary>RN-15: idempotente — no duplica inscripciones existentes.</summary>
    public Result Enroll(string studentId, int enrollmentNumber, string studentName, TermNumber termAtEnrollment, IClock clock)
    {
        if (_enrollments.Any(e => e.StudentId == studentId))
        {
            return Result.Success(); // idempotente: ya estaba inscrito, no es un error.
        }

        if (_enrollments.Count >= EmbeddedEnrollmentHardLimit)
        {
            return Result.Failure(Error.Conflict(
                "CourseOffering.CapacityExceeded",
                $"La oferta alcanzó el límite de {EmbeddedEnrollmentHardLimit} inscripciones embebidas; requiere migración a colección referenciada."));
        }

        _enrollments.Add(Enrollment.Create(studentId, enrollmentNumber, studentName, termAtEnrollment, clock));
        return Result.Success();
    }

    /// <summary>RN-16: solo el profesor dueño de la oferta o un administrador (verificado en Application).</summary>
    public Result RecordGrade(string studentId, Grade grade, string gradedByUserId, IClock clock)
    {
        var enrollment = _enrollments.FirstOrDefault(e => e.StudentId == studentId);
        if (enrollment is null)
        {
            return Result.Failure(Error.NotFound("CourseOffering.EnrollmentNotFound", "El alumno no está inscrito en esta oferta."));
        }

        var result = enrollment.RecordGrade(grade, gradedByUserId, clock);
        if (result.IsSuccess)
        {
            Raise(new GradeRecorded(Guid.NewGuid(), clock.UtcNow, Id, studentId, grade.Value, gradedByUserId));
        }

        return result;
    }

    public bool ApproachingEmbeddedLimit => _enrollments.Count >= EmbeddedEnrollmentSoftLimit;
}

/// <summary>Referencia desnormalizada a Region dentro de Academics (evita dependencia cíclica de agregados).</summary>
public sealed record RegionRefAcademics(string Id, int Code, string Name);
