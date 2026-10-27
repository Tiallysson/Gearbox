using Gearbox.Interop;

namespace Gearbox.Modules.Notch.Logic;

/// <summary>O que não muda enquanto o PC está ligado: modelos.</summary>
public sealed record HardwareInfo(string? CpuName, string? BoardName, GraphicsAdapterInfo? MainGpu);

/// <summary>Uma leitura da aba "Sistema". Cada campo é <c>null</c> quando o Windows não informa.</summary>
/// <param name="ThermalCelsius">Zona térmica ACPI mais quente (aproxima CPU/placa; nem todo PC tem).</param>
public sealed record SystemSnapshot(
    double? CpuPercent,
    double? ThermalCelsius,
    MemoryStatus? Memory,
    double? GpuPercent,
    ulong? GpuUsedBytes,
    double? GpuCelsius);

/// <summary>Contadores de GPU do Windows, por placa (as instâncias trazem o LUID da placa no nome).</summary>
public static class GpuCounters
{
    /// <summary>
    /// Separa uma instância de <c>\GPU Engine(*)\Utilization Percentage</c>, como
    /// <c>pid_1234_luid_0x00000000_0x0000D1E5_phys_0_eng_3_engtype_3D</c>, em placa (<c>luid_0x..._0x...</c>) e motor
    /// (<c>phys_0_eng_3_engtype_3D</c>: o mesmo motor em processos diferentes tem o mesmo texto).
    /// </summary>
    public static bool TryParseEngine(string instance, out string luidKey, out string engine)
    {
        luidKey = engine = string.Empty;
        var luidStart = instance.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
        var physStart = instance.IndexOf("_phys_", StringComparison.OrdinalIgnoreCase);
        if (luidStart < 0 || physStart <= luidStart)
        {
            return false;
        }

        luidKey = instance[luidStart..physStart];
        engine = instance[(physStart + 1)..];
        return true;
    }

    /// <summary>
    /// Uso de cada placa (0–100), por LUID, a partir das amostras de <c>\GPU Engine(*)\Utilization Percentage</c>
    /// (uma por processo e por motor da placa: 3D, cópia, vídeo...).
    /// </summary>
    public static Dictionary<string, double> UsageByAdapter(IEnumerable<CounterSample> samples)
    {
        // 1) Soma os processos por motor: eles dividem o mesmo motor da placa.
        var perEngine = new Dictionary<(string Luid, string Engine), double>();
        foreach (var sample in samples)
        {
            if (!TryParseEngine(sample.Instance, out var luid, out var engine))
            {
                continue;
            }

            var key = (luid, engine);
            perEngine[key] = perEngine.GetValueOrDefault(key) + sample.Value;
        }

        // 2) Por placa, o motor mais ocupado (como o Gerenciador de Tarefas): motores trabalham em paralelo.
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var ((luid, _), usage) in perEngine)
        {
            if (!result.TryGetValue(luid, out var current) || usage > current)
            {
                result[luid] = usage;
            }
        }

        return result;
    }

    /// <summary>
    /// Memória dedicada em uso por placa, a partir de <c>\GPU Adapter Memory(*)\Dedicated Usage</c> (instâncias
    /// <c>luid_0x..._0x..._phys_N</c>; uma placa pode ter mais de uma parte física).
    /// </summary>
    public static Dictionary<string, double> MemoryByAdapter(IEnumerable<CounterSample> samples)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in samples)
        {
            var physStart = sample.Instance.IndexOf("_phys_", StringComparison.OrdinalIgnoreCase);
            var luid = physStart > 0 ? sample.Instance[..physStart] : sample.Instance;
            result[luid] = result.GetValueOrDefault(luid) + sample.Value;
        }

        return result;
    }
}

public static class Temperatures
{
    /// <summary>
    /// Zona térmica mais quente, em °C, a partir de <c>\Thermal Zone Information(*)\Temperature</c> (em Kelvin).
    /// Leituras fora de 1–125 °C são de zonas desligadas ou com defeito e ficam de fora.
    /// </summary>
    public static double? HottestZoneCelsius(IEnumerable<CounterSample> kelvinSamples)
    {
        double? hottest = null;
        foreach (var sample in kelvinSamples)
        {
            var celsius = sample.Value - 273.15;
            if (celsius is > 1 and <= 125 && (hottest is null || celsius > hottest))
            {
                hottest = celsius;
            }
        }

        return hottest;
    }
}
