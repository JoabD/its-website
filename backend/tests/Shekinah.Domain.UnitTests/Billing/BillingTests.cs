using Shekinah.Domain.Billing;
using Shekinah.Domain.SharedKernel;
using Shouldly;
using Xunit;

namespace Shekinah.Domain.UnitTests.Billing;

public class BillingTests
{
    [Fact(DisplayName = "RN_21_Grade_solo_acepta_0_a_10")]
    public void RN_16_Grade_rango_valido()
    {
        Grade.Create(-1).IsFailure.ShouldBeTrue();
        Grade.Create(11).IsFailure.ShouldBeTrue();
        Grade.Create(0).IsSuccess.ShouldBeTrue();
        Grade.Create(10).IsSuccess.ShouldBeTrue();
    }

    [Fact(DisplayName = "RN_19_No_se_emite_aviso_sin_meses_adeudados")]
    public void RN_19_Sin_deuda_no_hay_aviso()
    {
        var student = new StudentRef("s1", 1001, "Juan Pérez");
        var result = PaymentNotice.Issue(Guid.NewGuid().ToString(), student, 1, [], "period-1", "admin", new TestClock());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PaymentNotice.NoDebt");
    }

    [Fact(DisplayName = "RN_19_Cuarto_aviso_resulta_en_bloqueo")]
    public void RN_19_Cuarto_aviso_bloquea()
    {
        var student = new StudentRef("s1", 1001, "Juan Pérez");
        var monthsDue = new[] { MonthCode.Create("202601").Value };

        var fourthNotice = PaymentNotice.Issue(Guid.NewGuid().ToString(), student, 4, monthsDue, "period-1", "admin", new TestClock()).Value;
        var thirdNotice = PaymentNotice.Issue(Guid.NewGuid().ToString(), student, 3, monthsDue, "period-1", "admin", new TestClock()).Value;

        fourthNotice.ResultedInBlock.ShouldBeTrue();
        thirdNotice.ResultedInBlock.ShouldBeFalse();
    }

    [Fact(DisplayName = "RN_17_DelinquencyPolicy_calcula_meses_no_pagados")]
    public void RN_17_DelinquencyPolicy_calcula_diferencia()
    {
        var periodMonths = new[] { "202601", "202602", "202603" }.Select(m => MonthCode.Create(m).Value).ToList();
        var paidMonths = new[] { MonthCode.Create("202601").Value };

        var due = DelinquencyPolicy.CalculateMonthsDue(periodMonths, paidMonths);

        due.Select(m => m.Value).ShouldBe(["202602", "202603"]);
        DelinquencyPolicy.ShouldNotify(due).ShouldBeTrue();
    }
}
