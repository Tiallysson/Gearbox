using Gearbox.Interop;
using Gearbox.Modules.Wallpaper.Logic;

namespace Gearbox.Modules.Wallpaper.Tests;

public sealed class CoverageRulesTests
{
    // Dois monitores 1920x1080 lado a lado, barra de tarefas de 48 px embaixo.
    private static readonly MonitorArea s_left = new("left", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1032));
    private static readonly MonitorArea s_right = new("right", new PixelRect(1920, 0, 3840, 1080), new PixelRect(1920, 0, 3840, 1032));

    private static DesktopWindow Window(PixelRect bounds, string className = "Notepad") =>
        new(bounds, className, IsVisible: true, IsMinimized: false, IsCloaked: false, IsToolWindow: false, IsClickThrough: false, IsOwnProcess: false);

    [Fact]
    public void TelaCheia_Cobre()
    {
        Assert.True(CoverageRules.Covers(new PixelRect(0, 0, 1920, 1080), s_left));
    }

    [Fact]
    public void Maximizada_ComBordasInvisiveisParaFora_Cobre()
    {
        // GetWindowRect de uma janela maximizada passa ~8 px de cada lado da área de trabalho.
        Assert.True(CoverageRules.Covers(new PixelRect(-8, -8, 1928, 1040), s_left));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    public void PseudoMaximizadaPeloRoundedMaximize_Cobre(int margin)
    {
        // Moldura visível na margem, com 7 px de borda invisível nas laterais e embaixo.
        var rect = new PixelRect(margin - 7, margin, 1920 - margin + 7, 1032 - margin + 7);

        Assert.True(CoverageRules.Covers(rect, s_left));
    }

    [Fact]
    public void JanelaComum_NaoCobre()
    {
        Assert.False(CoverageRules.Covers(new PixelRect(200, 150, 1400, 900), s_left));
    }

    [Fact]
    public void MetadeDaTela_NaoCobre()
    {
        Assert.False(CoverageRules.Covers(new PixelRect(0, 0, 960, 1032), s_left));
    }

    [Fact]
    public void CoveredMonitors_SoOMonitorDaJanela()
    {
        var windows = new[] { Window(new PixelRect(1912, -8, 3848, 1040)) };

        var covered = CoverageRules.CoveredMonitors(windows, [s_left, s_right]);

        Assert.Equal(["right"], covered);
    }

    [Theory]
    [InlineData("Progman")]
    [InlineData("WorkerW")]
    [InlineData("Shell_TrayWnd")]
    public void JanelasDoShell_SaoIgnoradas(string className)
    {
        var windows = new[] { Window(new PixelRect(0, 0, 3840, 1080), className) };

        Assert.Empty(CoverageRules.CoveredMonitors(windows, [s_left, s_right]));
    }

    [Fact]
    public void JanelasQueNaoAparecem_SaoIgnoradas()
    {
        var full = new PixelRect(0, 0, 1920, 1080);
        var windows = new[]
        {
            Window(full) with { IsVisible = false },
            Window(full) with { IsMinimized = true },
            Window(full) with { IsCloaked = true },
            Window(full) with { IsToolWindow = true },
            Window(full) with { IsClickThrough = true }, // overlays transparentes (ex.: NVIDIA)
            Window(full) with { IsOwnProcess = true },
        };

        Assert.Empty(CoverageRules.CoveredMonitors(windows, [s_left]));
    }
}
