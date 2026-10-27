using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.Audio.Endpoints;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Gearbox.Interop;

/// <summary>Volume do dispositivo de saída padrão.</summary>
/// <param name="Level">De 0 a 1.</param>
public readonly record struct VolumeLevel(float Level, bool IsMuted);

/// <summary>
/// Avisa quando o volume principal (ou o mudo) do dispositivo de saída padrão muda, por qualquer meio: teclas
/// de mídia, barra de tarefas, outro app.
/// </summary>
/// <remarks>
/// Precisa ser criado, atualizado (<see cref="Refresh"/>) e descartado na mesma thread. Os eventos chegam numa
/// thread do Core Audio: quem assina precisa levá-los para a própria thread.
/// </remarks>
public sealed class AudioVolumeMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly VolumeCallback _volumeCallback;
    private readonly DeviceCallback _deviceCallback;
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _endpoint;

    public AudioVolumeMonitor(ILogger logger)
    {
        _logger = logger;
        _volumeCallback = new VolumeCallback(this);
        _deviceCallback = new DeviceCallback(this);

        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        _enumerator.RegisterEndpointNotificationCallback(_deviceCallback);
        Refresh();
    }

    /// <summary>O volume ou o mudo mudou. Disparado numa thread do Core Audio.</summary>
    public event EventHandler<VolumeLevel>? VolumeChanged;

    /// <summary>O dispositivo de saída padrão mudou: chame <see cref="Refresh"/> na thread dona. Disparado numa thread do Core Audio.</summary>
    public event EventHandler? DefaultDeviceChanged;

    /// <summary>Uma saída de áudio foi conectada, removida, ativada ou desativada. Disparado numa thread do Core Audio.</summary>
    public event EventHandler? DevicesChanged;

    /// <summary>Volume atual, ou <c>null</c> se não há dispositivo de saída.</summary>
    public unsafe VolumeLevel? Current
    {
        get
        {
            if (_endpoint is null)
            {
                return null;
            }

            try
            {
                _endpoint.GetMasterVolumeLevelScalar(out var level);
                BOOL muted;
                _endpoint.GetMute(&muted);
                return new VolumeLevel(level, muted);
            }
            catch (COMException ex)
            {
                _logger.LogDebug(ex, "Não foi possível ler o volume");
                return null;
            }
        }
    }

    /// <summary>Muda o volume principal (0 a 1). Dispara <see cref="VolumeChanged"/> como qualquer outra mudança.</summary>
    public unsafe bool TrySetLevel(float level)
    {
        if (_endpoint is null || !float.IsFinite(level))
        {
            return false;
        }

        try
        {
            _endpoint.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), null);
            return true;
        }
        catch (COMException ex)
        {
            // O dispositivo sumiu entre a leitura e a escrita: o aviso de troca de dispositivo resolve.
            _logger.LogDebug(ex, "Não foi possível mudar o volume");
            return false;
        }
    }

    public unsafe bool TrySetMuted(bool muted)
    {
        if (_endpoint is null)
        {
            return false;
        }

        try
        {
            _endpoint.SetMute(muted, null);
            return true;
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "Não foi possível mudar o mudo");
            return false;
        }
    }

    /// <summary>Passa a acompanhar o dispositivo de saída padrão atual.</summary>
    public unsafe void Refresh()
    {
        DetachEndpoint();
        if (_enumerator is null)
        {
            return;
        }

        try
        {
            _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(&iid, CLSCTX.CLSCTX_ALL, null, out var endpoint);
            _endpoint = (IAudioEndpointVolume)endpoint;
            _endpoint.RegisterControlChangeNotify(_volumeCallback);
        }
        catch (COMException ex)
        {
            // Sem dispositivo de saída (E_NOTFOUND) é normal: o aviso de troca de dispositivo tenta de novo.
            _logger.LogDebug(ex, "Sem dispositivo de saída de áudio padrão");
            _endpoint = null;
        }
    }

    public void Dispose()
    {
        DetachEndpoint();
        if (_enumerator is not null)
        {
            try
            {
                _enumerator.UnregisterEndpointNotificationCallback(_deviceCallback);
            }
            catch (COMException ex)
            {
                _logger.LogDebug(ex, "Falha ao cancelar o aviso de dispositivos de áudio");
            }

            Marshal.ReleaseComObject(_enumerator);
            _enumerator = null;
        }
    }

    private void DetachEndpoint()
    {
        if (_endpoint is null)
        {
            return;
        }

        try
        {
            _endpoint.UnregisterControlChangeNotify(_volumeCallback);
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "Falha ao cancelar o aviso de volume");
        }

        Marshal.ReleaseComObject(_endpoint);
        _endpoint = null;
    }

    private sealed class VolumeCallback(AudioVolumeMonitor owner) : IAudioEndpointVolumeCallback
    {
        public unsafe void OnNotify(AUDIO_VOLUME_NOTIFICATION_DATA* pNotify)
        {
            if (pNotify is not null)
            {
                owner.VolumeChanged?.Invoke(owner, new VolumeLevel(pNotify->fMasterVolume, pNotify->bMuted));
            }
        }
    }

    private sealed class DeviceCallback(AudioVolumeMonitor owner) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, PCWSTR pwstrDefaultDeviceId)
        {
            if (flow == EDataFlow.eRender && role == ERole.eMultimedia)
            {
                owner.DefaultDeviceChanged?.Invoke(owner, EventArgs.Empty);
            }
        }

        public void OnDeviceStateChanged(PCWSTR pwstrDeviceId, DEVICE_STATE dwNewState) =>
            owner.DevicesChanged?.Invoke(owner, EventArgs.Empty);

        public void OnDeviceAdded(PCWSTR pwstrDeviceId) => owner.DevicesChanged?.Invoke(owner, EventArgs.Empty);

        public void OnDeviceRemoved(PCWSTR pwstrDeviceId) => owner.DevicesChanged?.Invoke(owner, EventArgs.Empty);

        public void OnPropertyValueChanged(PCWSTR pwstrDeviceId, PROPERTYKEY key)
        {
        }
    }
}

