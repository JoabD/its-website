using Shekinah.Domain.Identity;
using Shekinah.Domain.SharedKernel;
using Shouldly;
using Xunit;

namespace Shekinah.Domain.UnitTests.Identity;

public class UserTests
{
    private static PersonalProfile ValidProfile() => PersonalProfile.Create(
        PersonName.Create("Ana García").Value, Email.Create("ana@example.com").Value, PhoneNumber.Create("5512345678").Value,
        new DateOnly(1995, 5, 5), "Soltera", Address.Create("Calle 1", "Centro", "Loc", "Municipio").Value,
        ChurchInfo.Create("Iglesia Y", Address.Create("Calle 2", "Centro", "Loc", "Municipio").Value, "Pastor Y", "2 años", MinistryRole.None).Value,
        EducationLevel.Create(SchoolingLevel.HighSchool, null).Value, "Ninguna", "Servir").Value;

    private static RegionRef Region() => new(Guid.NewGuid().ToString(), 1, "Región Centro");

    [Fact(DisplayName = "RN_05_RN_24_Alumno_creado_desde_solicitud_requiere_cambio_de_password")]
    public void RN_05_RN_24_Requiere_cambio_de_password()
    {
        var user = User.CreateStudentFromApplication(
            Guid.NewGuid().ToString(), EnrollmentNumber.Create(1001).Value, "app-1", ValidProfile(),
            Modality.Onsite, Region(), "hashed-temp-password", new TestClock()).Value;

        user.Credentials.MustChangePassword.ShouldBeTrue();
        user.Academic!.CurrentTerm.Value.ShouldBe(1);
        user.Role.ShouldBe(UserRole.Student);
    }

    [Fact(DisplayName = "RN_07_Usuario_bloqueado_no_puede_autenticar")]
    public void RN_07_Usuario_bloqueado_no_autentica()
    {
        var user = User.CreateStudentFromApplication(
            Guid.NewGuid().ToString(), EnrollmentNumber.Create(1002).Value, "app-2", ValidProfile(),
            Modality.Onsite, Region(), "hash", new TestClock()).Value;

        for (var i = 0; i < 4; i++)
        {
            user.RegisterPaymentNotice(new TestClock());
        }

        user.Billing.IsBlocked.ShouldBeTrue();
        user.CanAuthenticate(out var reason).ShouldBeFalse();
        reason.ShouldBe("Usuario bloqueado por falta de pago.");
    }

    [Fact(DisplayName = "RN_19_Bloqueo_ocurre_exactamente_al_superar_3_avisos")]
    public void RN_19_Bloqueo_al_cuarto_aviso()
    {
        var user = User.CreateStudentFromApplication(
            Guid.NewGuid().ToString(), EnrollmentNumber.Create(1003).Value, "app-3", ValidProfile(),
            Modality.Onsite, Region(), "hash", new TestClock()).Value;

        user.RegisterPaymentNotice(new TestClock());
        user.Billing.IsBlocked.ShouldBeFalse();
        user.RegisterPaymentNotice(new TestClock());
        user.Billing.IsBlocked.ShouldBeFalse();
        user.RegisterPaymentNotice(new TestClock());
        user.Billing.IsBlocked.ShouldBeFalse();
        user.RegisterPaymentNotice(new TestClock());
        user.Billing.IsBlocked.ShouldBeTrue();
    }

    [Fact(DisplayName = "RN_18_Importar_pago_reinicia_contador_y_desbloquea")]
    public void RN_18_Importar_pago_desbloquea()
    {
        var user = User.CreateStudentFromApplication(
            Guid.NewGuid().ToString(), EnrollmentNumber.Create(1004).Value, "app-4", ValidProfile(),
            Modality.Onsite, Region(), "hash", new TestClock()).Value;

        for (var i = 0; i < 4; i++) user.RegisterPaymentNotice(new TestClock());
        user.Billing.IsBlocked.ShouldBeTrue();

        user.ResetDelinquency();

        user.Billing.IsBlocked.ShouldBeFalse();
        user.Billing.NoticeCount.ShouldBe(0);
        user.Status.ShouldBe(UserStatus.Active);
    }

    [Fact(DisplayName = "RN_14_Promocion_de_sexto_cuatrimestre_marca_egresado")]
    public void RN_14_Promocion_sexto_marca_egresado()
    {
        var user = User.CreateStudentFromApplication(
            Guid.NewGuid().ToString(), EnrollmentNumber.Create(1005).Value, "app-5", ValidProfile(),
            Modality.Onsite, Region(), "hash", new TestClock()).Value;

        for (var i = 0; i < 5; i++) user.PromoteTerm(new TestClock());

        user.Academic!.CurrentTerm.Value.ShouldBe(6);

        user.PromoteTerm(new TestClock());

        user.Academic!.IsGraduated.ShouldBeTrue();
    }
}
