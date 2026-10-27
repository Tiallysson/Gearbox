using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch;

/// <summary>Gravado em <c>%LocalAppData%\Gearbox\settings\Notch.json</c>.</summary>
public sealed class NotchSettings
{
    /// <summary>Música/vídeo tocando (qualquer app integrado aos controles de mídia do Windows).</summary>
    public bool ShowMedia { get; set; } = true;

    /// <summary>Aviso ao mudar o volume ou o mudo.</summary>
    public bool ShowVolume { get; set; } = true;

    /// <summary>Aviso ao ligar/desligar o carregador e nível da bateria na ilha aberta.</summary>
    public bool ShowBattery { get; set; } = true;

    /// <summary>Aba "Controles": botões de Wi‑Fi e Bluetooth.</summary>
    public bool ShowConnectivity { get; set; } = true;

    /// <summary>Aba "Controles": slider de volume, mudo e troca da saída de áudio.</summary>
    public bool ShowAudioControls { get; set; } = true;

    /// <summary>Aba "Armazenamento": uso de cada disco em anéis; clique abre o disco no Explorer.</summary>
    public bool ShowStorage { get; set; } = true;

    /// <summary>Aba "Sistema": processador, placa de vídeo, memória, temperaturas e placa-mãe.</summary>
    public bool ShowSystemInfo { get; set; } = true;

    /// <summary>Fundo acrílico: desfoca o que está atrás da ilha (o tom preto fica por cima).</summary>
    public bool AcrylicBackground { get; set; } = true;

    /// <summary>Borda de 1 px quase transparente em volta da ilha (efeito de vidro).</summary>
    public bool GlassBorder { get; set; } = true;

    /// <summary>
    /// Opacidade do tom preto da ilha, em % (20–100). Com o fundo acrílico, quanto menor, mais o desfoque aparece.
    /// O conteúdo continua opaco.
    /// </summary>
    public int BackgroundOpacity { get; set; } = NotchAppearance.DefaultOpacity;

    /// <summary>Esconde a ilha com um app em tela cheia (jogos, vídeos, apresentações).</summary>
    public bool HideOnFullscreen { get; set; } = true;

    /// <summary>Recolhida, vira só uma alça quando a janela em foco passa por baixo dela.</summary>
    public bool CompactWhenCovered { get; set; } = true;
}
