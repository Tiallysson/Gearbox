using Gearbox.Modules.Wallpaper.Logic;

namespace Gearbox.Modules.Wallpaper.Tests;

public sealed class PausePolicyTests
{
    private static readonly SystemState s_idle = new(OnBattery: false, SessionLocked: false, DisplayOff: false);

    [Fact]
    public void NadaAcontecendo_Toca()
    {
        Assert.False(PausePolicy.IsPaused(new WallpaperSettings(), s_idle, monitorCovered: false));
    }

    [Fact]
    public void MonitorCoberto_Pausa_SoSeAOpcaoEstiverLigada()
    {
        Assert.True(PausePolicy.IsPaused(new WallpaperSettings(), s_idle, monitorCovered: true));
        Assert.False(PausePolicy.IsPaused(new WallpaperSettings { PauseOnFullscreen = false }, s_idle, monitorCovered: true));
    }

    [Fact]
    public void NaBateria_Pausa_SoSeAOpcaoEstiverLigada()
    {
        var state = s_idle with { OnBattery = true };

        Assert.True(PausePolicy.IsPaused(new WallpaperSettings(), state, monitorCovered: false));
        Assert.False(PausePolicy.IsPaused(new WallpaperSettings { PauseOnBattery = false }, state, monitorCovered: false));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BloqueadaOuTelaApagada_Pausa_SoSeAOpcaoEstiverLigada(bool locked, bool displayOff)
    {
        var state = s_idle with { SessionLocked = locked, DisplayOff = displayOff };

        Assert.True(PausePolicy.IsPaused(new WallpaperSettings(), state, monitorCovered: false));
        Assert.False(PausePolicy.IsPaused(new WallpaperSettings { PauseWhenLocked = false }, state, monitorCovered: false));
    }
}
