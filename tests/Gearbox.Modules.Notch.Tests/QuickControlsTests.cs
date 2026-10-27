using Gearbox.Interop;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests;

public sealed class QuickControlsTests
{
    [Theory]
    [InlineData(RadioPower.On, RadioPower.Off)]
    [InlineData(RadioPower.Off, RadioPower.On)]
    public void ToggleTarget_Inverte(RadioPower current, RadioPower expected)
    {
        Assert.Equal(expected, QuickControls.ToggleTarget(new RadioInfo(current)));
    }

    [Fact]
    public void ToggleTarget_SemRadio_NaoFazNada()
    {
        Assert.Null(QuickControls.ToggleTarget(RadioInfo.Unavailable));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FromSlider_AcimaDeZero_TiraOMudo(bool wasMuted)
    {
        var target = QuickControls.FromSlider(new VolumeLevel(0.2f, wasMuted), 37);

        Assert.Equal(0.37f, target.Level, 0.0001f);
        Assert.False(target.IsMuted);
    }

    [Fact]
    public void FromSlider_EmZero_AtivaOMudo()
    {
        var target = QuickControls.FromSlider(new VolumeLevel(0.5f, IsMuted: false), 0);

        Assert.Equal(0f, target.Level);
        Assert.True(target.IsMuted);
    }

    [Theory]
    [InlineData(-20, 0f, true)]
    [InlineData(150, 1f, false)]
    public void FromSlider_ForaDaFaixa_ELimitado(double percent, float expectedLevel, bool expectedMuted)
    {
        var target = QuickControls.FromSlider(new VolumeLevel(0.5f, IsMuted: false), percent);

        Assert.Equal(expectedLevel, target.Level);
        Assert.Equal(expectedMuted, target.IsMuted);
    }

    [Fact]
    public void ShapeFor_AbertaComAbas_EMaisAlta()
    {
        var plain = NotchLayout.ShapeFor(NotchMode.Expanded, isPlaying: true, compact: false, withTabs: false);
        var tabbed = NotchLayout.ShapeFor(NotchMode.Expanded, isPlaying: true, compact: false, withTabs: true);

        Assert.Equal(NotchLayout.ExpandedShape, plain);
        Assert.Equal(NotchLayout.ExpandedTabbedShape, tabbed);
        Assert.True(tabbed.Size.Height > plain.Size.Height);
    }

    [Theory]
    [InlineData(NotchMode.Collapsed)]
    [InlineData(NotchMode.Peek)]
    public void ShapeFor_AbasSoAfetamAAberta(NotchMode mode)
    {
        Assert.Equal(
            NotchLayout.ShapeFor(mode, isPlaying: false, compact: false, withTabs: false),
            NotchLayout.ShapeFor(mode, isPlaying: false, compact: false, withTabs: true));
    }
}
