using Gearbox.Interop;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests;

public sealed class OverlapRulesTests
{
    // Monitor 1920x1080; ilha recolhida de 236x30 no topo, centralizada.
    private static readonly PixelRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PixelRect s_island = new(842, 0, 1078, 30);

    private static ForegroundWindow Window(PixelRect bounds, string className = "Chrome_WidgetWin_1", bool own = false) =>
        new(bounds, className, IsMinimized: false, IsCloaked: false, IsOwnProcess: own);

    private static NotchVisibility Evaluate(
        ForegroundWindow? foreground,
        UserNotificationState state = UserNotificationState.AcceptsNotifications,
        bool hideOnFullscreen = true,
        bool compactWhenCovered = true) =>
        OverlapRules.Evaluate(state, foreground, s_monitor, s_island, hideOnFullscreen, compactWhenCovered);

    [Fact]
    public void SemJanela_Normal()
    {
        Assert.Equal(NotchVisibility.Normal, Evaluate(null));
    }

    [Fact]
    public void JanelaLongeDaIlha_Normal()
    {
        Assert.Equal(NotchVisibility.Normal, Evaluate(Window(new PixelRect(100, 200, 900, 800))));
    }

    [Fact]
    public void Maximizada_Compacta()
    {
        // GetWindowRect de uma janela maximizada passa ~8 px de cada lado.
        Assert.Equal(NotchVisibility.Compact, Evaluate(Window(new PixelRect(-8, -8, 1928, 1040))));
    }

    [Fact]
    public void JanelaQueSoEncostaNaIlha_Normal()
    {
        // Começa exatamente onde a ilha termina: não há sobreposição.
        Assert.Equal(NotchVisibility.Normal, Evaluate(Window(new PixelRect(500, 30, 1500, 900))));
    }

    [Fact]
    public void Maximizada_SemCompactar_Normal()
    {
        Assert.Equal(NotchVisibility.Normal, Evaluate(Window(new PixelRect(-8, -8, 1928, 1040)), compactWhenCovered: false));
    }

    [Fact]
    public void TelaCheia_Esconde()
    {
        Assert.Equal(NotchVisibility.Hidden, Evaluate(Window(new PixelRect(0, 0, 1920, 1080))));
    }

    [Fact]
    public void TelaCheia_SemEsconder_Compacta()
    {
        Assert.Equal(NotchVisibility.Compact, Evaluate(Window(new PixelRect(0, 0, 1920, 1080)), hideOnFullscreen: false));
    }

    [Theory]
    [InlineData(UserNotificationState.Busy)]
    [InlineData(UserNotificationState.RunningDirect3DFullScreen)]
    [InlineData(UserNotificationState.PresentationMode)]
    public void ShellOcupado_Esconde(UserNotificationState state)
    {
        Assert.Equal(NotchVisibility.Hidden, Evaluate(null, state));
    }

    [Fact]
    public void ShellOcupado_SemEsconder_Normal()
    {
        Assert.Equal(NotchVisibility.Normal, Evaluate(null, UserNotificationState.RunningDirect3DFullScreen, hideOnFullscreen: false));
    }

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Shell_TrayWnd")]
    public void AreaDeTrabalhoEBarraDeTarefas_SaoIgnoradas(string className)
    {
        Assert.Equal(NotchVisibility.Normal, Evaluate(Window(new PixelRect(0, 0, 1920, 1080), className)));
    }

    [Fact]
    public void JanelaDoProprioGearbox_EIgnorada()
    {
        Assert.Equal(NotchVisibility.Normal, Evaluate(Window(new PixelRect(-8, -8, 1928, 1040), own: true)));
    }

    [Fact]
    public void Minimizada_EIgnorada()
    {
        var window = Window(new PixelRect(0, 0, 1920, 1080)) with { IsMinimized = true };

        Assert.Equal(NotchVisibility.Normal, Evaluate(window));
    }

    [Fact]
    public void Cloaked_EIgnorada()
    {
        var window = Window(new PixelRect(0, 0, 1920, 1080)) with { IsCloaked = true };

        Assert.Equal(NotchVisibility.Normal, Evaluate(window));
    }
}
