using Gearbox.Interop;
using Gearbox.Modules.RoundedMaximize.Logic;

namespace Gearbox.Modules.RoundedMaximize.Tests;

public sealed class PseudoMaximizeLayoutTests
{
    // 1920x1080 com barra de tarefas de 48 px embaixo.
    private static readonly PixelRect s_workArea = new(0, 0, 1920, 1032);

    [Fact]
    public void SemBordasInvisiveis_AplicaMargemEmTodosOsLados()
    {
        var rect = PseudoMaximizeLayout.Compute(s_workArea, 8, FrameInsets.None);

        Assert.Equal(new PixelRect(8, 8, 1912, 1024), rect);
    }

    [Fact]
    public void ComBordasInvisiveis_AMolduraVisivelFicaNaMargem()
    {
        // Bordas típicas do Windows 11 a 100%: 7 px nas laterais e embaixo, 0 no topo.
        var insets = new FrameInsets(7, 0, 7, 7);

        var rect = PseudoMaximizeLayout.Compute(s_workArea, 8, insets);

        Assert.Equal(new PixelRect(1, 8, 1919, 1031), rect);

        var visible = new PixelRect(rect.Left + insets.Left, rect.Top + insets.Top, rect.Right - insets.Right, rect.Bottom - insets.Bottom);
        Assert.Equal(new PixelRect(8, 8, 1912, 1024), visible);
    }

    [Fact]
    public void MargemZero_OcupaAAreaDeTrabalhoInteira()
    {
        Assert.Equal(s_workArea, PseudoMaximizeLayout.Compute(s_workArea, 0, FrameInsets.None));
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(64, 64)]
    [InlineData(500, 64)]
    public void Margem_EhLimitadaEntre0E64(int requested, int expected)
    {
        var rect = PseudoMaximizeLayout.Compute(s_workArea, requested, FrameInsets.None);

        Assert.Equal(expected, rect.Left - s_workArea.Left);
        Assert.Equal(expected, s_workArea.Bottom - rect.Bottom);
    }

    [Fact]
    public void MonitorSecundarioComCoordenadasNegativas()
    {
        var secondary = new PixelRect(-2560, -200, 0, 1240);

        var rect = PseudoMaximizeLayout.Compute(secondary, 16, FrameInsets.None);

        Assert.Equal(new PixelRect(-2544, -184, -16, 1224), rect);
    }

    [Fact]
    public void AreaPequenaDemaisParaAMargem_UsaAreaInteira()
    {
        var tiny = PixelRect.FromSize(0, 0, 100, 60);

        Assert.Equal(tiny, PseudoMaximizeLayout.Compute(tiny, 64, FrameInsets.None));
    }
}
