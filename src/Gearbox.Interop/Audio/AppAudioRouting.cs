using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.WinRT;

namespace Gearbox.Interop.Audio;

public static class AppAudioRouting
{
    private const string PolicyConfigClass = "Windows.Media.Internal.AudioPolicyConfig";
    private const string DevicePrefix = @"\\?\SWD#MMDEVAPI#";
    private const string RenderInterfaceSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    public static unsafe bool TrySetOutput(uint processId, string? deviceId, ILogger logger)
    {
        if (processId == 0)
        {
            return false;
        }

        object? factory = null;
        try
        {
            factory = CreateFactory();
            if (factory is null)
            {
                logger.LogWarning("Saída de áudio por app indisponível nesta versão do Windows");
                return false;
            }

            WindowsDeleteStringSafeHandle? target = null;
            try
            {
                var handle = (nint)0;
                if (deviceId is not null)
                {
                    var full = DevicePrefix + deviceId + RenderInterfaceSuffix;
                    PInvoke.WindowsCreateString(full, (uint)full.Length, out target).ThrowOnFailure();
                    handle = target.DangerousGetHandle();
                }

                foreach (var role in new[] { ERole.eConsole, ERole.eMultimedia })
                {
                    var hr = factory switch
                    {
                        IAudioPolicyConfigFactory current => current.SetPersistedDefaultAudioEndpoint(processId, EDataFlow.eRender, role, handle),
                        IAudioPolicyConfigFactoryLegacy legacy => legacy.SetPersistedDefaultAudioEndpoint(processId, EDataFlow.eRender, role, handle),
                        _ => -1,
                    };
                    if (hr < 0)
                    {
                        logger.LogWarning("Falha ao trocar a saída de áudio do processo {Process} (HRESULT 0x{Hr:X8})", processId, hr);
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                target?.Dispose();
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            logger.LogWarning(ex, "Saída de áudio por app indisponível nesta versão do Windows");
            return false;
        }
        finally
        {
            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }
    }

    public static bool IsSupported => FactoryInterfaceFor(Environment.OSVersion.Version.Build) is not null;

    internal static Type? FactoryInterfaceFor(int build) => build switch
    {
        >= FirstBuild and < CurrentLayoutBuild => typeof(IAudioPolicyConfigFactoryLegacy),
        >= CurrentLayoutBuild and < FirstUnknownBuild => typeof(IAudioPolicyConfigFactory),
        _ => null,
    };

    private const int FirstBuild = 10240;
    private const int CurrentLayoutBuild = 21390;
    private const int FirstUnknownBuild = 28000;

    private static unsafe object? CreateFactory()
    {
        if (FactoryInterfaceFor(Environment.OSVersion.Version.Build) is not { } expected)
        {
            return null;
        }

        PInvoke.WindowsCreateString(PolicyConfigClass, (uint)PolicyConfigClass.Length, out var classId).ThrowOnFailure();
        using (classId)
        {
            var id = expected.GUID;
            var hr = PInvoke.RoGetActivationFactory((HSTRING)classId.DangerousGetHandle(), &id, out var factory);
            return hr.Succeeded ? factory : null;
        }
    }

    [ComImport]
    [Guid("ab3d4648-e242-459f-b02f-541c70306324")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioPolicyConfigFactory
    {
        [PreserveSig] int GetIids();
        [PreserveSig] int GetRuntimeClassName();
        [PreserveSig] int GetTrustLevel();
        [PreserveSig] int AddContextVolumeChange();
        [PreserveSig] int RemoveContextVolumeChange();
        [PreserveSig] int AddRingerVibrateStateChanged();
        [PreserveSig] int RemoveRingerVibrateStateChanged();
        [PreserveSig] int SetVolumeGroupGainForId();
        [PreserveSig] int GetVolumeGroupGainForId();
        [PreserveSig] int GetActiveVolumeGroupForEndpointId();
        [PreserveSig] int GetVolumeGroupsForEndpoint();
        [PreserveSig] int GetCurrentVolumeContext();
        [PreserveSig] int SetVolumeGroupMuteForId();
        [PreserveSig] int GetVolumeGroupMuteForId();
        [PreserveSig] int SetRingerVibrateState();
        [PreserveSig] int GetRingerVibrateState();
        [PreserveSig] int SetPreferredChatApplication();
        [PreserveSig] int ResetPreferredChatApplication();
        [PreserveSig] int GetPreferredChatApplication();
        [PreserveSig] int GetCurrentChatApplications();
        [PreserveSig] int AddChatContextChanged();
        [PreserveSig] int RemoveChatContextChanged();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, nint deviceId);
    }

    [ComImport]
    [Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioPolicyConfigFactoryLegacy
    {
        [PreserveSig] int GetIids();
        [PreserveSig] int GetRuntimeClassName();
        [PreserveSig] int GetTrustLevel();
        [PreserveSig] int AddContextVolumeChange();
        [PreserveSig] int RemoveContextVolumeChange();
        [PreserveSig] int AddRingerVibrateStateChanged();
        [PreserveSig] int RemoveRingerVibrateStateChanged();
        [PreserveSig] int SetVolumeGroupGainForId();
        [PreserveSig] int GetVolumeGroupGainForId();
        [PreserveSig] int GetActiveVolumeGroupForEndpointId();
        [PreserveSig] int GetVolumeGroupsForEndpoint();
        [PreserveSig] int GetCurrentVolumeContext();
        [PreserveSig] int SetVolumeGroupMuteForId();
        [PreserveSig] int GetVolumeGroupMuteForId();
        [PreserveSig] int SetRingerVibrateState();
        [PreserveSig] int GetRingerVibrateState();
        [PreserveSig] int SetPreferredChatApplication();
        [PreserveSig] int ResetPreferredChatApplication();
        [PreserveSig] int GetPreferredChatApplication();
        [PreserveSig] int GetCurrentChatApplications();
        [PreserveSig] int AddChatContextChanged();
        [PreserveSig] int RemoveChatContextChanged();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, nint deviceId);
    }
}
