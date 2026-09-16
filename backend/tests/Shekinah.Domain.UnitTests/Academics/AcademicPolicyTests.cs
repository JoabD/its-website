using Shekinah.Domain.Academics;
using Shekinah.Domain.Academics.Policies;
using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;
using Shouldly;
using Xunit;

namespace Shekinah.Domain.UnitTests.Academics;

public class AcademicPolicyTests
{
    [Fact(DisplayName = "RN_17_Meses_de_cobro_son_la_serie_YYYYMM_inclusive_hasta_hoy")]
    public void RN_17_Calcula_meses_de_cobro()
    {
        var range = DateRange.Create(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)).Value;
        var now = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);

        var months = AcademicPeriodMonthsCalculator.Calculate(range, now);

        months.Select(m => m.Value).ShouldBe(["202601", "202602", "202603", "202604"]);
    }

    [Fact(DisplayName = "RN_13_Cerrar_un_periodo_ya_cerrado_falla")]
    public void RN_13_Periodo_cerrado_no_se_reabre()
    {
        var range = DateRange.Create(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)).Value;
        var period = AcademicPeriod.Open(Guid.NewGuid().ToString(), PeriodCode.Create("20260101").Value, "Enero-Abril", range, "admin", new TestClock()).Value;

        period.Close("admin", new TestClock()).IsSuccess.ShouldBeTrue();
        var secondClose = period.Close("admin", new TestClock());

        secondClose.IsFailure.ShouldBeTrue();
        secondClose.Error.Code.ShouldBe("AcademicPeriod.AlreadyClosed");
    }

    [Fact(DisplayName = "RN_22_Ofertas_duplicadas_no_se_permiten_a_nivel_de_agregado")]
    public void RN_16_Calificar_alumno_no_inscrito_falla()
    {
        var offering = CourseOffering.Create(
            Guid.NewGuid().ToString(),
            new PeriodRef("p1", "20260101"),
            new SubjectRef("s1", "Bibliología", 1),
            new RegionRefAcademics("r1", 1, "Región Centro"),
            new TeacherRef("t1", 500, "Profesor X")).Value;

        var result = offering.RecordGrade("alumno-inexistente", Grade.Create(8).Value, "t1", new TestClock());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CourseOffering.EnrollmentNotFound");
    }

    [Fact(DisplayName = "RN_15_Matriculacion_automatica_es_idempotente")]
    public void RN_15_Enroll_es_idempotente()
    {
        var offering = CourseOffering.Create(
            Guid.NewGuid().ToString(),
            new PeriodRef("p1", "20260101"),
            new SubjectRef("s1", "Bibliología", 1),
            new RegionRefAcademics("r1", 1, "Región Centro"),
            new TeacherRef("t1", 500, "Profesor X")).Value;

        offering.Enroll("student-1", 1001, "Juan Pérez", TermNumber.Create(1).Value, new TestClock());
        offering.Enroll("student-1", 1001, "Juan Pérez", TermNumber.Create(1).Value, new TestClock());

        offering.EnrollmentCount.ShouldBe(1);
    }

    [Fact(DisplayName = "RN_15_Solo_alumnos_del_cuatrimestre_y_region_de_la_oferta_son_elegibles")]
    public void RN_15_Elegibilidad_por_cuatrimestre_y_region()
    {
        var offering = CourseOffering.Create(
            Guid.NewGuid().ToString(),
            new PeriodRef("p1", "20260101"),
            new SubjectRef("s1", "Bibliología", 1),
            new RegionRefAcademics("region-A", 1, "Región A"),
            new TeacherRef("t1", 500, "Profesor X")).Value;

        var matchingStudent = User.CreateStudentFromApplication(
            "student-a", EnrollmentNumber.Create(2001).Value, "app-a",
            TestProfiles.Valid(), Modality.Onsite, new RegionRef("region-A", 1, "Región A"), "hash", new TestClock()).Value;

        var wrongRegionStudent = User.CreateStudentFromApplication(
            "student-b", EnrollmentNumber.Create(2002).Value, "app-b",
            TestProfiles.Valid(), Modality.Onsite, new RegionRef("region-B", 2, "Región B"), "hash", new TestClock()).Value;

        AutoEnrollmentPolicy.IsEligible(matchingStudent, offering).ShouldBeTrue();
        AutoEnrollmentPolicy.IsEligible(wrongRegionStudent, offering).ShouldBeFalse();
    }
}

internal static class TestProfiles
{
    public static PersonalProfile Valid() => PersonalProfile.Create(
        PersonName.Create("Test User").Value, Email.Create("test@example.com").Value, PhoneNumber.Create("5512345678").Value,
        new DateOnly(1990, 1, 1), "Soltero", Address.Create("Calle 1", "Centro", "Loc", "Municipio").Value,
        ChurchInfo.Create("Iglesia", Address.Create("Calle 2", "Centro", "Loc", "Municipio").Value, "Pastor", "1 año", MinistryRole.None).Value,
        EducationLevel.Create(SchoolingLevel.HighSchool, null).Value, "Ninguna", "Servir").Value;
}
