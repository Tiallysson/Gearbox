namespace Gearbox.Modules.Notch.Logic;

/// <summary>Como a ilha está sendo exibida.</summary>
public enum NotchMode
{
    /// <summary>Pílula pequena (ou só uma alça, quando cobriria uma janela).</summary>
    Collapsed,

    /// <summary>Aviso temporário: volume, bateria, faixa nova.</summary>
    Peek,

    /// <summary>Aberta pelo mouse: mídia com controles, relógio e bateria.</summary>
    Expanded,
}

/// <summary>O que um aviso temporário mostra.</summary>
public enum PeekKind
{
    None,
    Volume,
    Battery,
    Track,
}

/// <summary>
/// Estado da ilha: aberta enquanto o mouse está sobre ela; senão mostra o aviso ativo; senão fica recolhida.
/// Os atrasos (abrir, fechar, fim do aviso) ficam com quem usa: aqui só as transições.
/// </summary>
public sealed class NotchState
{
    public bool IsHovered { get; private set; }

    public PeekKind Peek { get; private set; }

    public NotchMode Mode => IsHovered ? NotchMode.Expanded : Peek != PeekKind.None ? NotchMode.Peek : NotchMode.Collapsed;

    /// <summary>Devolve <c>true</c> se mudou.</summary>
    public bool SetHovered(bool hovered)
    {
        if (IsHovered == hovered)
        {
            return false;
        }

        IsHovered = hovered;

        // Quem abriu a ilha já viu o aviso: ele não volta ao fechar.
        if (hovered)
        {
            Peek = PeekKind.None;
        }

        return true;
    }

    /// <summary>Mostra um aviso (substitui o anterior). Com a ilha aberta, é ignorado. Devolve <c>true</c> se mudou.</summary>
    public bool ShowPeek(PeekKind kind)
    {
        if (IsHovered || kind == PeekKind.None)
        {
            return false;
        }

        Peek = kind;
        return true;
    }

    /// <summary>Encerra o aviso, se ainda for o mesmo. Devolve <c>true</c> se mudou.</summary>
    public bool EndPeek(PeekKind kind)
    {
        if (Peek != kind || kind == PeekKind.None)
        {
            return false;
        }

        Peek = PeekKind.None;
        return true;
    }

    /// <summary>Some com o aviso que não pode mais aparecer (ex.: o tipo foi desligado nas configurações).</summary>
    public bool ClearPeek()
    {
        if (Peek == PeekKind.None)
        {
            return false;
        }

        Peek = PeekKind.None;
        return true;
    }
}
