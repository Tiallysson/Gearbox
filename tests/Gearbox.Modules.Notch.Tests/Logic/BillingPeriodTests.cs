using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class BillingPeriodTests
{
    [Fact]
    public void InicioDoMes_EmUtc()
    {
        var start = BillingPeriod.MonthStart(new DateTimeOffset(2026, 11, 17, 15, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void NoiteDoUltimoDiaNoBrasil_JaEOMesSeguinteEmUtc()
    {
        var start = BillingPeriod.MonthStart(new DateTimeOffset(2026, 11, 30, 22, 0, 0, TimeSpan.FromHours(-3)));

        Assert.Equal(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), start);
    }
}
