using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class PageTransitionsTests
{
    [Theory]
    [InlineData(NotchPage.Controls, NotchPage.Devices)]
    [InlineData(NotchPage.Controls, NotchPage.Mixer)]
    [InlineData(NotchPage.Controls, NotchPage.Network)]
    [InlineData(NotchPage.Mixer, NotchPage.MixerDevices)]
    public void IsDrillIn_SeletoresDeSaidaEMixer_AnimamIgual(NotchPage from, NotchPage to)
    {
        Assert.True(PageTransitions.IsDrillIn(from, to));
    }

    [Theory]
    [InlineData(NotchPage.Devices, NotchPage.Controls)]
    [InlineData(NotchPage.Mixer, NotchPage.Controls)]
    [InlineData(NotchPage.MixerDevices, NotchPage.Mixer)]
    [InlineData(NotchPage.Media, NotchPage.Controls)]
    [InlineData(NotchPage.Mixer, NotchPage.Mixer)]
    public void IsDrillIn_VoltarOuTrocarDeAba_NaoAnima(NotchPage from, NotchPage to)
    {
        Assert.False(PageTransitions.IsDrillIn(from, to));
    }
}
