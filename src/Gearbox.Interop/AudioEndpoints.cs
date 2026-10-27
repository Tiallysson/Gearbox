using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;

namespace Gearbox.Interop;

/// <summary>Dispositivo de saída de áudio ativo (fone, caixa, HDMI...).</summary>
/// <param name="Id">Id do endpoint: opaco, só serve para devolver a <see cref="AudioEndpoints.TrySetDefaultOutput"/>.</param>
public sealed record AudioOutputDevice(string Id, string Name, bool IsDefault);

/// <summary>Lista as saídas de áudio e troca a padrão.</summary>
/// <remarks>
/// O Windows não tem API pública para trocar o dispositivo padrão: a troca usa <c>IPolicyConfig</c>, a interface COM
/// não documentada que o próprio painel de som usa (e o EarTrumpet, entre outros). Ela existe desde o Windows 7 com
/// o mesmo layout, mas pode mudar numa atualização: por isso cada chamada é protegida e uma falha só devolve
/// <c>false</c>. É a única declaração COM escrita à mão do projeto, porque não está nos metadados do Win32.
/// Chame na thread de UI (as mesmas regras do <see cref="AudioVolumeMonitor"/>).
/// </remarks>
public static class AudioEndpoints
{
    /// <summary>Saídas ativas, na ordem do Windows. Vazia se o serviço de áudio não responde.</summary>
    public static List<AudioOutputDevice> GetOutputDevices(ILogger logger)
    {
        var devices = new List<AudioOutputDevice>();
        IMMDeviceEnumerator? enumerator = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var defaultId = TryGetDefaultId(enumerator);
            enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE.DEVICE_STATE_ACTIVE, out var collection);
            try
            {
                collection.GetCount(out var count);
                for (uint i = 0; i < count; i++)
                {
                    collection.Item(i, out var device);
                    try
                    {
                        var id = GetId(device);
                        devices.Add(new AudioOutputDevice(id, GetFriendlyName(device) ?? id, id == defaultId));
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(collection);
            }
        }
        catch (COMException ex)
        {
            logger.LogDebug(ex, "Não foi possível listar as saídas de áudio");
        }
        finally
        {
            if (enumerator is not null)
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }

        return devices;
    }

    /// <summary>Torna <paramref name="deviceId"/> a saída padrão (para apps e para o sistema, como o painel de som faz).</summary>
    public static bool TrySetDefaultOutput(string deviceId, ILogger logger)
    {
        IPolicyConfig? policy = null;
        try
        {
            policy = (IPolicyConfig)new PolicyConfigClient();
            foreach (var role in new[] { ERole.eConsole, ERole.eMultimedia })
            {
                var hr = policy.SetDefaultEndpoint(deviceId, role);
                if (hr < 0)
                {
                    logger.LogWarning("Falha ao trocar a saída de áudio padrão (HRESULT 0x{Hr:X8})", hr);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            // InvalidCast: o Windows deixou de expor a interface com esse IID.
            logger.LogWarning(ex, "Troca de saída de áudio indisponível nesta versão do Windows");
            return false;
        }
        finally
        {
            if (policy is not null)
            {
                Marshal.ReleaseComObject(policy);
            }
        }
    }

    private static string? TryGetDefaultId(IMMDeviceEnumerator enumerator)
    {
        try
        {
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            try
            {
                return GetId(device);
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        catch (COMException)
        {
            return null; // Sem saída padrão (E_NOTFOUND).
        }
    }

    private static unsafe string GetId(IMMDevice device)
    {
        PWSTR id;
        device.GetId(&id);
        try
        {
            return id.ToString();
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)id.Value);
        }
    }

    private static unsafe string? GetFriendlyName(IMMDevice device)
    {
        device.OpenPropertyStore(STGM.STGM_READ, out var store);
        try
        {
            store.GetValue(PInvoke.PKEY_Device_FriendlyName, out var value);
            try
            {
                PInvoke.PropVariantToStringAlloc(value, out var text).ThrowOnFailure();
                try
                {
                    var name = text.ToString();
                    return string.IsNullOrWhiteSpace(name) ? null : name;
                }
                finally
                {
                    Marshal.FreeCoTaskMem((nint)text.Value);
                }
            }
            finally
            {
                PInvoke.PropVariantClear(ref value);
            }
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient;

    /// <summary>
    /// Só <see cref="SetDefaultEndpoint"/> é usado; os métodos antes dele existem para manter a ordem da vtable e
    /// nunca são chamados (por isso sem parâmetros).
    /// </summary>
    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    }
}
