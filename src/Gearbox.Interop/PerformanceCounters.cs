using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Performance;

namespace Gearbox.Interop;

/// <summary>Valor de uma instância de contador (ex.: uma zona térmica, um motor de GPU de um processo).</summary>
public readonly record struct CounterSample(string Instance, double Value);

/// <summary>
/// Consulta de contadores de desempenho do Windows (PDH), os mesmos do Monitor de Desempenho. Caminhos em inglês
/// (<c>PdhAddEnglishCounter</c>): funcionam em qualquer idioma do Windows.
/// </summary>
/// <remarks>
/// Contadores de taxa (uso em %) precisam de duas coletas: a primeira leitura não tem valor. Use uma instância por
/// thread (o PDH não é thread-safe por consulta).
/// </remarks>
public sealed class PerformanceCounters : IDisposable
{
    private const uint ErrorSuccess = 0;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhCstatusValidData = 0;
    private const uint PdhCstatusNewData = 1;

    private readonly PdhCloseQuerySafeHandle _query;
    private readonly List<PDH_HCOUNTER> _counters = [];

    public PerformanceCounters()
    {
        var status = PInvoke.PdhOpenQuery(null, 0, out _query);
        if (status != ErrorSuccess)
        {
            throw new COMException("PdhOpenQuery falhou", unchecked((int)status));
        }
    }

    /// <summary>
    /// Adiciona um contador (pode ter <c>*</c> na instância). Devolve o índice para <see cref="Read"/>, ou <c>-1</c>
    /// se o contador não existe nesta máquina (ex.: sem zonas térmicas ACPI).
    /// </summary>
    public int Add(string englishPath)
    {
        if (PInvoke.PdhAddEnglishCounter(_query, englishPath, 0, out var counter) != ErrorSuccess)
        {
            return -1;
        }

        _counters.Add(counter);
        return _counters.Count - 1;
    }

    /// <summary>Coleta uma amostra de todos os contadores. Devolve <c>false</c> se a coleta falhou.</summary>
    public bool Collect() => PInvoke.PdhCollectQueryData(new PDH_HQUERY(_query.DangerousGetHandle())) == ErrorSuccess;

    /// <summary>Valores da última coleta, por instância; vazio se o contador ainda não tem valor válido.</summary>
    public unsafe List<CounterSample> Read(int index)
    {
        var samples = new List<CounterSample>();
        if (index < 0 || index >= _counters.Count)
        {
            return samples;
        }

        var counter = _counters[index];
        uint bufferSize = 0;
        uint count;
        var status = PInvoke.PdhGetFormattedCounterArray(counter, PDH_FMT.PDH_FMT_DOUBLE, &bufferSize, &count, null);
        if (status != PdhMoreData || bufferSize == 0)
        {
            return samples;
        }

        // Os nomes das instâncias apontam para dentro do próprio buffer: ele precisa ficar fixo até lermos tudo.
        var buffer = new byte[bufferSize];
        fixed (byte* bytes = buffer)
        {
            var items = (PDH_FMT_COUNTERVALUE_ITEM_W*)bytes;
            if (PInvoke.PdhGetFormattedCounterArray(counter, PDH_FMT.PDH_FMT_DOUBLE, &bufferSize, &count, items) != ErrorSuccess)
            {
                return samples;
            }

            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                if (item.FmtValue.CStatus is PdhCstatusValidData or PdhCstatusNewData)
                {
                    samples.Add(new CounterSample(item.szName.ToString(), item.FmtValue.doubleValue));
                }
            }
        }

        return samples;
    }

    public void Dispose() => _query.Dispose();
}
