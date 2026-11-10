using System.Text.Json.Serialization;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch;

/// <summary>Gravado em <c>%LocalAppData%\Gearbox\settings\Notch.json</c>.</summary>
public sealed class NotchSettings : IJsonOnDeserialized
{
    private BackgroundStyle _backgroundStyle = BackgroundStyle.Glass;
    private bool _backgroundStyleRead;
    private bool? _legacyAcrylic;
    private int? _legacyOpacity;

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

    public bool ShowMixer { get; set; } = true;

    /// <summary>Aba "Armazenamento": uso de cada disco em anéis; clique abre o disco no Explorer.</summary>
    public bool ShowStorage { get; set; } = true;

    /// <summary>Aba "Sistema": processador, placa de vídeo, memória, temperaturas e placa-mãe.</summary>
    public bool ShowSystemInfo { get; set; } = true;

    public bool ShowClipboard { get; set; } = true;

    public bool ShowLinks { get; set; } = true;

    public BackgroundStyle BackgroundStyle
    {
        get => _backgroundStyle;
        set
        {
            _backgroundStyle = value;
            _backgroundStyleRead = true;
        }
    }

    public string BackgroundColor { get; set; } = NotchAppearance.Black.ToHex();

    public bool UseAccentColor { get; set; }

    public NotchRoundness Roundness { get; set; } = NotchRoundness.Default;

    public bool SpringAnimation { get; set; } = true;

    public SpringIntensity SpringIntensity { get; set; } = SpringIntensity.Normal;

    /// <summary>Borda de 1 px quase transparente em volta da ilha (efeito de vidro).</summary>
    public bool GlassBorder { get; set; } = true;

    public bool AdaptTextContrast { get; set; } = true;

    [JsonInclude]
    [JsonPropertyName("acrylicBackground")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private bool? LegacyAcrylicBackground
    {
        get => null;
        set => _legacyAcrylic = value;
    }

    [JsonInclude]
    [JsonPropertyName("backgroundOpacity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int? LegacyBackgroundOpacity
    {
        get => null;
        set => _legacyOpacity = value;
    }

    /// <summary>Esconde a ilha com um app em tela cheia (jogos, vídeos, apresentações).</summary>
    public bool HideOnFullscreen { get; set; } = true;

    /// <summary>Recolhida, vira só uma alça quando a janela em foco passa por baixo dela.</summary>
    public bool CompactWhenCovered { get; set; } = true;

    public bool CollapseToHandle { get; set; } = true;

    public bool ShowMediaWhenCollapsed { get; set; } = true;

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (!_backgroundStyleRead && (_legacyAcrylic is not null || _legacyOpacity is not null))
        {
            _backgroundStyle = NotchAppearance.StyleFromLegacy(_legacyAcrylic ?? true, _legacyOpacity ?? 60);
        }

        _legacyAcrylic = null;
        _legacyOpacity = null;
    }
}
