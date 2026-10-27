namespace Gearbox.Modules.Notch.Logic;

/// <summary>Aparência da ilha escolhida pelo usuário.</summary>
public static class NotchAppearance
{
    /// <summary>Com o fundo acrílico (padrão), deixa o desfoque aparecer sem perder o visual escuro.</summary>
    public const int DefaultOpacity = 60;

    /// <summary>Abaixo disso o fundo some contra fundos claros e o texto branco fica ilegível.</summary>
    public const int MinOpacity = 20;

    public const int MaxOpacity = 100;

    /// <summary>Opacidade do fundo (%) dentro da faixa permitida.</summary>
    public static int ClampOpacity(int percent) => Math.Clamp(percent, MinOpacity, MaxOpacity);

    /// <summary>Opacidade do fundo como fração (0.2–1.0), para o pincel.</summary>
    public static double ToBrushOpacity(int percent) => ClampOpacity(percent) / 100.0;
}
