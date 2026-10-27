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
    public void TryApplySlider_NoMudo_ReduzNivelAntesDeLiberarAudio()
    {
        var actual = new VolumeLevel(1f, true);
        var audible = new List<float>();
        var calls = new List<string>();

        var success = QuickControls.TryApplySlider(actual, 10,
            level =>
            {
                calls.Add("level");
                actual = actual with { Level = level };
                if (!actual.IsMuted) audible.Add(actual.Level);
                return true;
            },
            muted =>
            {
                calls.Add("mute");
                actual = actual with { IsMuted = muted };
                if (!muted) audible.Add(actual.Level);
                return true;
            });

        Assert.True(success);
        Assert.Equal(["level", "mute"], calls);
        Assert.Equal([0.1f], audible);
    }

    [Fact]
    public void TryApplySlider_FalhaAoMudarNivel_NaoRetiraMudo()
    {
        var muteCalls = 0;
        var success = QuickControls.TryApplySlider(new VolumeLevel(1f, true), 10,
            _ => false,
            _ => { muteCalls++; return true; });

        Assert.False(success);
        Assert.Equal(0, muteCalls);
    }

    [Fact]
    public void TryApplySlider_FalhaAoMudarMudo_RetornaFalha()
    {
        Assert.False(QuickControls.TryApplySlider(new VolumeLevel(1f, true), 10, _ => true, _ => false));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TryApplySlider_ValorNaoFinito_NaoAlteraAudio(double percent)
    {
        var calls = 0;
        Assert.False(QuickControls.TryApplySlider(new VolumeLevel(0.5f, true), percent,
            _ => { calls++; return true; }, _ => { calls++; return true; }));
        Assert.Equal(0, calls);
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

