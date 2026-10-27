using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Wdk.Graphics.Direct3D;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace Gearbox.Interop;

/// <summary>Placa de vídeo física (o adaptador de software do Windows fica de fora).</summary>
/// <param name="LuidKey">
/// Identificador no formato das instâncias dos contadores de GPU do Windows (<c>luid_0x{alto}_0x{baixo}</c>), para
/// casar com o uso e a memória lidos via <see cref="PerformanceCounters"/>.
/// </param>
public sealed record GraphicsAdapterInfo(string Name, string LuidKey, ulong DedicatedBytes, uint LuidLow, int LuidHigh);

/// <summary>Placas de vídeo (DXGI) e temperatura delas (D3DKMT, a mesma fonte do Gerenciador de Tarefas).</summary>
public static class GraphicsAdapters
{
    public static unsafe List<GraphicsAdapterInfo> Enumerate(ILogger logger)
    {
        var adapters = new List<GraphicsAdapterInfo>();
        IDXGIFactory1? factory = null;
        try
        {
            var iid = typeof(IDXGIFactory1).GUID;
            PInvoke.CreateDXGIFactory1(&iid, out var created).ThrowOnFailure();
            factory = (IDXGIFactory1)created;
            for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Succeeded; i++)
            {
                try
                {
                    var desc = adapter.GetDesc1();
                    if ((desc.Flags & DXGI_ADAPTER_FLAG.DXGI_ADAPTER_FLAG_SOFTWARE) != 0)
                    {
                        continue;
                    }

                    var luid = desc.AdapterLuid;
                    adapters.Add(new GraphicsAdapterInfo(
                        desc.Description.ToString().Trim(),
                        $"luid_0x{luid.HighPart:X8}_0x{luid.LowPart:X8}",
                        desc.DedicatedVideoMemory,
                        luid.LowPart,
                        luid.HighPart));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        catch (COMException ex)
        {
            logger.LogDebug(ex, "Não foi possível listar as placas de vídeo");
        }
        finally
        {
            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }

        return adapters;
    }

    /// <summary>
    /// Temperatura da GPU em °C, ou <c>null</c> se o driver não informa (WDDM 2.4+; placas integradas costumam não
    /// informar). Não precisa de administrador.
    /// </summary>
    public static unsafe double? TryGetTemperature(GraphicsAdapterInfo adapter)
    {
        var open = new D3DKMT_OPENADAPTERFROMLUID { AdapterLuid = new LUID { LowPart = adapter.LuidLow, HighPart = adapter.LuidHigh } };
        if (Windows.Wdk.PInvoke.D3DKMTOpenAdapterFromLuid(ref open).Value < 0)
        {
            return null;
        }

        try
        {
            var perf = new D3DKMT_ADAPTER_PERFDATA { PhysicalAdapterIndex = 0 };
            var query = new D3DKMT_QUERYADAPTERINFO
            {
                hAdapter = open.hAdapter,
                Type = KMTQUERYADAPTERINFOTYPE.KMTQAITYPE_ADAPTERPERFDATA,
                pPrivateDriverData = &perf,
                PrivateDriverDataSize = (uint)sizeof(D3DKMT_ADAPTER_PERFDATA),
            };

            // Temperature vem em décimos de grau Celsius; 0 = o driver não informa.
            return Windows.Wdk.PInvoke.D3DKMTQueryAdapterInfo(ref query).Value >= 0 && perf.Temperature > 0
                ? perf.Temperature / 10.0
                : null;
        }
        finally
        {
            Windows.Wdk.PInvoke.D3DKMTCloseAdapter(new D3DKMT_CLOSEADAPTER { hAdapter = open.hAdapter });
        }
    }
}
