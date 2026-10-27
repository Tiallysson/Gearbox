using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests;

public sealed class NotchStateTests
{
    [Fact]
    public void ComecaRecolhida()
    {
        var state = new NotchState();

        Assert.Equal(NotchMode.Collapsed, state.Mode);
        Assert.Equal(PeekKind.None, state.Peek);
    }

    [Fact]
    public void MouseEmCima_Abre()
    {
        var state = new NotchState();

        Assert.True(state.SetHovered(true));
        Assert.Equal(NotchMode.Expanded, state.Mode);
    }

    [Fact]
    public void MesmoHover_NaoMuda()
    {
        var state = new NotchState();
        state.SetHovered(true);

        Assert.False(state.SetHovered(true));
    }

    [Fact]
    public void Aviso_MostraEDepoisRecolhe()
    {
        var state = new NotchState();

        Assert.True(state.ShowPeek(PeekKind.Volume));
        Assert.Equal(NotchMode.Peek, state.Mode);

        Assert.True(state.EndPeek(PeekKind.Volume));
        Assert.Equal(NotchMode.Collapsed, state.Mode);
    }

    [Fact]
    public void AvisoNovo_SubstituiOAnterior()
    {
        var state = new NotchState();
        state.ShowPeek(PeekKind.Volume);

        state.ShowPeek(PeekKind.Battery);

        Assert.Equal(PeekKind.Battery, state.Peek);
    }

    [Fact]
    public void FimDeUmAvisoAntigo_NaoEncerraOAtual()
    {
        var state = new NotchState();
        state.ShowPeek(PeekKind.Volume);
        state.ShowPeek(PeekKind.Battery);

        Assert.False(state.EndPeek(PeekKind.Volume));
        Assert.Equal(PeekKind.Battery, state.Peek);
    }

    [Fact]
    public void ComAIlhaAberta_AvisoEIgnorado()
    {
        var state = new NotchState();
        state.SetHovered(true);

        Assert.False(state.ShowPeek(PeekKind.Volume));
        Assert.Equal(NotchMode.Expanded, state.Mode);
    }

    [Fact]
    public void AbrirDuranteUmAviso_DescartaOAviso()
    {
        var state = new NotchState();
        state.ShowPeek(PeekKind.Track);

        state.SetHovered(true);
        state.SetHovered(false);

        Assert.Equal(NotchMode.Collapsed, state.Mode);
    }

    [Fact]
    public void AvisoNone_NaoFazNada()
    {
        var state = new NotchState();

        Assert.False(state.ShowPeek(PeekKind.None));
        Assert.False(state.EndPeek(PeekKind.None));
    }

    [Fact]
    public void ClearPeek_SoMudaSeHaviaAviso()
    {
        var state = new NotchState();
        Assert.False(state.ClearPeek());

        state.ShowPeek(PeekKind.Battery);

        Assert.True(state.ClearPeek());
        Assert.Equal(NotchMode.Collapsed, state.Mode);
    }
}
