using Gearbox.Interop;
using Gearbox.Modules.RoundedMaximize.Logic;

namespace Gearbox.Modules.RoundedMaximize.Tests;

public sealed class PseudoMaximizeTrackerTests
{
    private const nint Hwnd = 0x42;

    private static readonly PixelRect s_original = new(100, 100, 900, 700);
    private static readonly PixelRect s_pseudoNormal = new(8, 8, 1912, 1032);
    private static readonly PixelRect s_pseudoScreen = new(1, 8, 1919, 1039);

    private readonly PseudoMaximizeTracker _tracker = new();

    [Fact]
    public void PrimeiroMaximizar_EhPseudoMaximizar()
    {
        Assert.Equal(MaximizeAction.PseudoMaximize, _tracker.OnMaximized(Hwnd, s_original));
    }

    [Fact]
    public void MaximizarDeNovoNoRetanguloPseudo_RestauraOOriginal()
    {
        Track();

        Assert.Equal(MaximizeAction.RestoreOriginal, _tracker.OnMaximized(Hwnd, s_pseudoNormal));
    }

    [Fact]
    public void ToleraDiferencaDeUmPixel()
    {
        Track();

        var almost = s_pseudoNormal with { Right = s_pseudoNormal.Right + 1 };

        Assert.Equal(MaximizeAction.RestoreOriginal, _tracker.OnMaximized(Hwnd, almost));
    }

    [Fact]
    public void MovidaAntesDeMaximizar_EhMaximizarNovo()
    {
        Track();
        var moved = s_pseudoNormal with { Left = 300, Right = 1100 };

        Assert.Equal(MaximizeAction.PseudoMaximize, _tracker.OnMaximized(Hwnd, moved));
        Assert.False(_tracker.TryGet(Hwnd, out _));
    }

    [Fact]
    public void MovimentoParaForaDoPseudo_DeixaDeRastrear()
    {
        Track();

        var left = _tracker.OnMovedWhileNotMaximized(Hwnd, new PixelRect(50, 50, 600, 400), new PixelRect(43, 50, 607, 407));

        Assert.True(left);
        Assert.Equal(0, _tracker.Count);
    }

    [Fact]
    public void EventoSemMudanca_ContinuaRastreando()
    {
        Track();

        var left = _tracker.OnMovedWhileNotMaximized(Hwnd, s_pseudoNormal, s_pseudoScreen);

        Assert.False(left);
        Assert.Equal(1, _tracker.Count);
    }

    [Fact]
    public void Minimizada_ComparaSoONormalPosition()
    {
        Track();

        Assert.False(_tracker.OnMovedWhileNotMaximized(Hwnd, s_pseudoNormal, windowRect: null));
    }

    [Fact]
    public void SnapMantemNormalPositionMasMudaORetanguloDaJanela_DeixaDeRastrear()
    {
        Track();

        // No Aero Snap o rcNormalPosition continua o mesmo, mas a janela ocupa outra área.
        Assert.True(_tracker.OnMovedWhileNotMaximized(Hwnd, s_pseudoNormal, new PixelRect(-7, 0, 967, 1047)));
    }

    [Fact]
    public void JanelaNaoRastreada_MovimentoEhIgnorado()
    {
        Assert.False(_tracker.OnMovedWhileNotMaximized(Hwnd, s_original, s_original));
    }

    [Fact]
    public void Forget_RemoveAJanela()
    {
        Track();

        Assert.True(_tracker.Forget(Hwnd));
        Assert.Equal(MaximizeAction.PseudoMaximize, _tracker.OnMaximized(Hwnd, s_pseudoNormal));
    }

    [Fact]
    public void Windows_DevolveUmaCopia()
    {
        Track();

        foreach (var window in _tracker.Windows)
        {
            _tracker.Forget(window.Hwnd);
        }

        Assert.Equal(0, _tracker.Count);
    }

    [Fact]
    public void OriginalWindowRect_SemDeslocamento_EhONormalPosition()
    {
        var tracked = CreateTracked(pseudoNormal: new PixelRect(1, 8, 1919, 1031), pseudoScreen: new PixelRect(1, 8, 1919, 1031));

        Assert.Equal(s_original, tracked.OriginalWindowRect);
    }

    [Fact]
    public void OriginalWindowRect_BarraDeTarefasNoTopo_AplicaODeslocamentoDoWorkspace()
    {
        // Barra de tarefas de 48 px em cima: workspace (0,0) corresponde à tela (0,48).
        var tracked = CreateTracked(pseudoNormal: new PixelRect(1, 8, 1919, 1031), pseudoScreen: new PixelRect(1, 56, 1919, 1079));

        Assert.Equal(new PixelRect(100, 148, 900, 748), tracked.OriginalWindowRect);
    }

    private static TrackedWindow CreateTracked(PixelRect pseudoNormal, PixelRect pseudoScreen) =>
        new(Hwnd, new WindowPlacement(ShowWindowCommand.ShowMaximized, s_original, 0, -1, -1, -1, -1), pseudoNormal, pseudoScreen);

    private void Track()
    {
        var placement = new WindowPlacement(ShowWindowCommand.ShowMaximized, s_original, 0, -1, -1, -1, -1);
        _tracker.Track(new TrackedWindow(Hwnd, placement, s_pseudoNormal, s_pseudoScreen));
    }
}
