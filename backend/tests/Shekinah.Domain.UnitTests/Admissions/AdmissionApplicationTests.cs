using Shekinah.Domain.Admissions;
using Shekinah.Domain.Catalog;
using Shekinah.Domain.SharedKernel;
using Shouldly;
using Xunit;

namespace Shekinah.Domain.UnitTests.Admissions;

public class AdmissionApplicationTests
{
    private static ApplicantProfile ValidApplicant() => ApplicantProfile.Create(
        PersonName.Create("Juan Pérez López").Value, new DateOnly(1990, 1, 1), "Casado",
        Email.Create("juan@example.com").Value, PhoneNumber.Create("5512345678").Value,
        Address.Create("Calle 1", "Centro", "Loc", "Municipio", "Estado").Value,
        ChurchInfo.Create("Iglesia X", Address.Create("Calle 2", "Centro", "Loc", "Municipio").Value, "Pastor X", "5 años", MinistryRole.None).Value,
        EducationLevel.Create(SchoolingLevel.HighSchool, null).Value, "Ninguna", "Servir a Dios").Value;

    private static Region OnsiteRegion() => Region.Create(Guid.NewGuid().ToString(), 1, "Región Centro", [Modality.Onsite]).Value;

    [Fact(DisplayName = "RN_01_Solicitud_decidida_es_inmutable")]
    public void RN_01_Solicitud_decidida_es_inmutable()
    {
        var region = OnsiteRegion();
        var modality = ModalityChoice.Create(Modality.Onsite, region, null).Value;
        var application = AdmissionApplication.Submit(Guid.NewGuid().ToString(), "SOL-2026-000001", ValidApplicant(), modality, [], new TestClock()).Value;

        application.Approve("admin-1", "Admin", "user-1", new TestClock()).IsSuccess.ShouldBeTrue();

        var secondDecision = application.Reject("admin-1", "Admin", "no cumple", new TestClock());

        secondDecision.IsFailure.ShouldBeTrue();
        secondDecision.Error.Code.ShouldBe("AdmissionApplication.NotPending");
    }

    [Fact(DisplayName = "RN_02_RN_03_Modalidad_Online_requiere_motivo_y_region_virtual")]
    public void RN_02_RN_03_Modalidad_Online_requiere_motivo()
    {
        var onlineRegion = Region.Create(Guid.NewGuid().ToString(), 3, "Región Virtual", [Modality.Online]).Value;

        var withoutReason = ModalityChoice.Create(Modality.Online, onlineRegion, null);
        withoutReason.IsFailure.ShouldBeTrue();
        withoutReason.Error.Code.ShouldBe("ModalityChoice.OnlineReasonRequired");

        var withReason = ModalityChoice.Create(Modality.Online, onlineRegion, "Vivo lejos de la sede");
        withReason.IsSuccess.ShouldBeTrue();
    }

    [Fact(DisplayName = "RN_03_Region_incoherente_con_modalidad_es_rechazada")]
    public void RN_03_Region_incoherente_es_rechazada()
    {
        var virtualRegion = Region.Create(Guid.NewGuid().ToString(), 3, "Región Virtual", [Modality.Online]).Value;

        var result = ModalityChoice.Create(Modality.Onsite, virtualRegion, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ModalityChoice.RegionMismatch");
    }

    [Fact(DisplayName = "RN_06_Rechazar_solicitud_no_crea_usuario")]
    public void RN_06_Rechazar_no_crea_usuario()
    {
        var region = OnsiteRegion();
        var modality = ModalityChoice.Create(Modality.Onsite, region, null).Value;
        var application = AdmissionApplication.Submit(Guid.NewGuid().ToString(), "SOL-2026-000002", ValidApplicant(), modality, [], new TestClock()).Value;

        var result = application.Reject("admin-1", "Admin", "Documentación incompleta", new TestClock());

        result.IsSuccess.ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.Rejected);
        application.CreatedUserId.ShouldBeNull();
    }
}
