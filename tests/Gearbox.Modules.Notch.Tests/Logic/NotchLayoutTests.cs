using Gearbox.Interop.Geometry;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class NotchLayoutTests
{
    private static readonly PixelRect s_monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(NotchVisibility.Normal, true, false, true)]
    [InlineData(NotchVisibility.Compact, true, false, true)]
    [InlineData(NotchVisibility.Normal, false, false, false)]
    [InlineData(NotchVisibility.Compact, false, false, true)]
    [InlineData(NotchVisibility.Normal, true, true, false)]
    [InlineData(NotchVisibility.Compact, true, true, true)]
    public void IsCompact_MusicaTocandoMostraAPilulaForaDeJanelas(NotchVisibility visibility, bool collapseToHandle, bool showsMedia, bool expected)
    {
        Assert.Equal(expected, NotchLayout.IsCompact(visibility, collapseToHandle, showsMedia));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShowsCollapsedMedia_SoTocandoEComAOpcaoLigada(bool isPlaying, bool option, bool expected)
    {
        Assert.Equal(expected, NotchLayout.ShowsCollapsedMedia(isPlaying, option));
    }

    [Fact]
    public void Place_CentralizaNoTopo()
    {
        var rect = NotchLayout.Place(s_monitor, 1.0, new DipSize(200, 30));

        Assert.Equal(new PixelRect(860, 0, 1060, 30), rect);
    }

    [Fact]
    public void Place_AplicaEscalaDeDpi()
    {
        var rect = NotchLayout.Place(s_monitor, 1.5, new DipSize(200, 30));

        Assert.Equal(300, rect.Width);
        Assert.Equal(45, rect.Height);
        Assert.Equal(810, rect.Left);
    }

    [Fact]
    public void Place_ArredondaParaCima()
    {
        // 30 * 1.25 = 37.5: a janela não pode cortar a forma.
        var rect = NotchLayout.Place(s_monitor, 1.25, new DipSize(180, 30));

        Assert.Equal(225, rect.Width);
        Assert.Equal(38, rect.Height);
    }

    [Fact]
    public void Place_MonitorSecundario_UsaAOrigemDele()
    {
        var rect = NotchLayout.Place(new PixelRect(1920, -200, 4480, 1240), 1.0, new DipSize(200, 30));

        Assert.Equal(new PixelRect(3100, -200, 3300, -170), rect);
    }

    [Fact]
    public void Place_EscalaInvalida_UsaUm()
    {
        var rect = NotchLayout.Place(s_monitor, 0, new DipSize(200, 30));

        Assert.Equal(200, rect.Width);
    }

    [Fact]
    public void ShapeFor_RecolhidaTocando_EMaiorQueParada()
    {
        var idle = NotchLayout.ShapeFor(NotchMode.Collapsed, isPlaying: false, compact: false);
        var playing = NotchLayout.ShapeFor(NotchMode.Collapsed, isPlaying: true, compact: false);

        Assert.Equal(NotchLayout.Idle, idle);
        Assert.Equal(NotchLayout.Playing, playing);
        Assert.True(playing.Size.Width > idle.Size.Width);
    }

    [Fact]
    public void ShapeFor_AbertaAlta_SoQuandoAberta()
    {
        Assert.Equal(NotchLayout.ExpandedTallShape, NotchLayout.ShapeFor(NotchMode.Expanded, isPlaying: false, compact: false, withTabs: true, tall: true));
        Assert.Equal(NotchLayout.Idle, NotchLayout.ShapeFor(NotchMode.Collapsed, isPlaying: false, compact: false, withTabs: true, tall: true));
    }

    [Fact]
    public void ShapeFor_ControlesCompletos_CresceUmPoucoSoAberta()
    {
        var roomy = NotchLayout.ShapeFor(NotchMode.Expanded, isPlaying: false, compact: false, withTabs: true, roomy: true);

        Assert.Equal(NotchLayout.ExpandedRoomyShape, roomy);
        Assert.True(roomy.Size.Height > NotchLayout.ExpandedTabbedShape.Size.Height);
        Assert.True(roomy.Size.Height < NotchLayout.ExpandedTallShape.Size.Height);
        Assert.Equal(NotchLayout.Idle, NotchLayout.ShapeFor(NotchMode.Collapsed, isPlaying: false, compact: false, withTabs: true, roomy: true));
    }

    [Fact]
    public void MaxWindowSize_CabeAMaiorFormaComArredondamentoMaximoEPicoDaMola()
    {
        var largest = NotchLayout.WindowSize(NotchLayout.WithRoundness(NotchLayout.ExpandedTallShape, NotchAppearance.MaxRoundnessFactor));
        var peak = 1 + SpringMotion.MaxOvershoot;

        Assert.True(NotchLayout.MaxWindowSize.Width >= largest.Width * peak);
        Assert.True(NotchLayout.MaxWindowSize.Height >= largest.Height * peak);
    }

    [Fact]
    public void WithRoundness_EscalaCantosEOrelhas()
    {
        var shape = NotchLayout.WithRoundness(NotchLayout.ExpandedShape, 1.5);

        Assert.Equal(42, shape.CornerRadius);
        Assert.Equal(21, shape.EarRadius);
        Assert.Equal(NotchLayout.ExpandedShape.Size, shape.Size);
    }

    [Fact]
    public void WithRoundness_CantoNaoPassaDaAltura()
    {
        var shape = NotchLayout.WithRoundness(NotchLayout.Handle, 10);

        Assert.Equal(NotchLayout.Handle.Size.Height, shape.CornerRadius);
    }

    [Fact]
    public void ShapeFor_Compacta_ViraAlca()
    {
        Assert.Equal(NotchLayout.Handle, NotchLayout.ShapeFor(NotchMode.Collapsed, isPlaying: true, compact: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShapeFor_AvisoEAberta_IgnoramACompactacao(bool compact)
    {
        Assert.Equal(NotchLayout.PeekShape, NotchLayout.ShapeFor(NotchMode.Peek, isPlaying: false, compact));
        Assert.Equal(NotchLayout.ExpandedShape, NotchLayout.ShapeFor(NotchMode.Expanded, isPlaying: false, compact));
    }

    [Fact]
    public void DipSizeMax_PegaOMaiorDeCadaLado()
    {
        Assert.Equal(new DipSize(300, 40), DipSize.Max(new DipSize(300, 20), new DipSize(100, 40)));
    }

    [Fact]
    public void WindowSize_SomaUmaOrelhaDeCadaLado()
    {
        Assert.Equal(new DipSize(196, 30), NotchLayout.WindowSize(new DipSize(180, 30), earRadius: 8));
    }

    [Fact]
    public void WindowSize_OrelhaNegativa_ContaComoZero()
    {
        Assert.Equal(new DipSize(180, 30), NotchLayout.WindowSize(new DipSize(180, 30), earRadius: -4));
    }

    public static TheoryData<NotchShape> AllShapes =>
    [
        NotchLayout.Idle,
        NotchLayout.Playing,
        NotchLayout.Handle,
        NotchLayout.PeekShape,
        NotchLayout.ExpandedShape,
        NotchLayout.ExpandedTabbedShape,
        NotchLayout.ExpandedRoomyShape,
        NotchLayout.ExpandedTallShape,
    ];

    public static TheoryData<NotchShape> CollapsedShapes =>
    [
        NotchLayout.Idle,
        NotchLayout.Playing,
        NotchLayout.Handle,
    ];

    [Theory]
    [MemberData(nameof(CollapsedShapes))]
    public void Recolhida_SemOrelhas_JanelaDoTamanhoDoCorpo(NotchShape shape)
    {
        // Recolhida fica no topo o tempo todo: não pode ocupar mais espaço que o corpo.
        Assert.Equal(0, shape.EarRadius);
        Assert.Equal(shape.Size, NotchLayout.WindowSize(shape));
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void Formas_OrelhaCabeNaAltura(NotchShape shape)
    {
        // A curva da orelha termina na lateral do corpo: mais alta que ele, sobraria uma ponta solta.
        Assert.InRange(shape.EarRadius, 0, shape.Size.Height);
    }
}
