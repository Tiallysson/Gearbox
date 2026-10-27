using Gearbox.Interop;

namespace Gearbox.Modules.Notch.Logic;

/// <summary>Rádios que a aba "Controles" liga e desliga.</summary>
public enum QuickRadio
{
    WiFi,
    Bluetooth,
}

public enum RadioPower
{
    /// <summary>O PC não tem esse rádio (ou o Windows negou acesso a ele).</summary>
    Unavailable,
    Off,
    On,
}

/// <summary>Estado de um rádio. <paramref name="NetworkName"/>: só Wi‑Fi, a rede conectada (se houver).</summary>
public readonly record struct RadioInfo(RadioPower Power, string? NetworkName = null)
{
    public static readonly RadioInfo Unavailable = new(RadioPower.Unavailable);
}

/// <summary>O que um botão da aba "Controles" pede ao módulo.</summary>
public enum QuickAction
{
    ToggleWiFi,
    ToggleBluetooth,

    /// <summary>Clique direito no Wi‑Fi: lista de redes do Windows.</summary>
    OpenWiFiList,

    /// <summary>Clique direito no Bluetooth: dispositivos Bluetooth nas Configurações.</summary>
    OpenBluetoothList,

    /// <summary>Clique no botão da rede quando a conexão é por cabo: configurações de Ethernet.</summary>
    OpenEthernetSettings,
    ToggleMute,
}

/// <summary>Regras dos controles rápidos (sem Win32: testáveis).</summary>
public static class QuickControls
{
    /// <summary>Estado que o botão do rádio deve pedir ao ser clicado, ou <c>null</c> se não há o que fazer.</summary>
    public static RadioPower? ToggleTarget(RadioInfo radio) => radio.Power switch
    {
        RadioPower.On => RadioPower.Off,
        RadioPower.Off => RadioPower.On,
        _ => null,
    };

    /// <summary>
    /// Volume que o sistema deve receber quando o usuário move o slider da ilha até <paramref name="percent"/> (0–100),
    /// partindo do volume <paramref name="current"/>.
    /// </summary>
    public static VolumeLevel FromSlider(VolumeLevel current, double percent)
    {
        if (!double.IsFinite(percent))
        {
            return current;
        }

        float level = Math.Clamp((float)(percent / 100.0), 0f, 1f);
        bool isMuted = level == 0;
        return new VolumeLevel(level, isMuted);
    }

    /// <summary>Aplica o nível ainda no mudo; uma falha ao ajustar o volume nunca libera o nível anterior.</summary>
    public static bool TryApplySlider(
        VolumeLevel current,
        double percent,
        Func<float, bool> setLevel,
        Func<bool, bool> setMuted)
    {
        if (!double.IsFinite(percent))
        {
            return false;
        }

        var target = FromSlider(current, percent);
        if (!setLevel(target.Level))
        {
            return false;
        }

        return target.IsMuted == current.IsMuted || setMuted(target.IsMuted);
    }
}

