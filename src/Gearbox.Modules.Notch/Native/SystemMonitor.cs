using Gearbox.Core.Threading;
using Gearbox.Interop;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;

namespace Gearbox.Modules.Notch.Native;

/// <summary>
/// Leituras da aba "Sistema": modelos (uma vez) e, enquanto ativo, uso de CPU, GPU e memória e temperaturas, a cada
/// segundo.
/// </summary>
/// <remarks>
/// A amostragem roda numa thread do pool (os contadores de GPU têm uma instância por processo e por motor: centenas)
/// e só enquanto a aba está na tela (<see cref="SetActive"/>). Os resultados vão para a thread de UI;
/// <see cref="HardwareChanged"/> e <see cref="SnapshotChanged"/> disparam nela. Falhas só são logadas: sem um
/// sensor, o campo fica vazio.
/// </remarks>
internal sealed class SystemMonitor : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly DispatcherQueue _dispatcher;
    private readonly ILogger _logger;
    private readonly BackgroundLoop _loop;
    private HardwareInfo? _hardware;
    private bool _disposed;

    public event EventHandler<HardwareInfo>? HardwareChanged;

    public event EventHandler<SystemSnapshot>? SnapshotChanged;

    public SystemMonitor(DispatcherQueue dispatcher, ILogger logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _loop = new BackgroundLoop(RunAsync, logger);
    }

    /// <summary>Liga (aba "Sistema" na tela) ou desliga a amostragem.</summary>
    public void SetActive(bool active) => _loop.SetActive(active);

    public void Dispose()
    {
        _disposed = true;
        _loop.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellation)
    {
        try
        {
            var hardware = _hardware ??= ReadHardware();
            Post(() =>
            {
                HardwareChanged?.Invoke(this, hardware);
            }, cancellation);

            using var counters = new PerformanceCounters();
            var cpu = counters.Add(@"\Processor Information(_Total)\% Processor Utility");
            var gpuEngines = counters.Add(@"\GPU Engine(*)\Utilization Percentage");
            var gpuMemory = counters.Add(@"\GPU Adapter Memory(*)\Dedicated Usage");
            var thermal = counters.Add(@"\Thermal Zone Information(*)\Temperature");

            // Contadores de taxa precisam de uma coleta anterior para ter valor.
            counters.Collect();
            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                if (!counters.Collect())
                {
                    continue;
                }

                var snapshot = Sample(counters, hardware.MainGpu, cpu, gpuEngines, gpuMemory, thermal);
                Post(() => SnapshotChanged?.Invoke(this, snapshot), cancellation);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Leitura do sistema interrompida");
        }
    }

    private SystemSnapshot Sample(PerformanceCounters counters, GraphicsAdapterInfo? gpu, int cpu, int gpuEngines, int gpuMemory, int thermal)
    {
        var cpuSamples = counters.Read(cpu);
        double? cpuPercent = cpuSamples.Count > 0 ? Math.Clamp(cpuSamples[0].Value, 0, 100) : null;

        double? gpuPercent = null;
        ulong? gpuUsed = null;
        double? gpuCelsius = null;
        if (gpu is not null)
        {
            try
            {
                gpuPercent = GpuCounters.UsageByAdapter(counters.Read(gpuEngines)).TryGetValue(gpu.LuidKey, out var usage)
                    ? Math.Clamp(usage, 0, 100)
                    : 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Falha ao calcular o uso da GPU");
            }

            if (GpuCounters.MemoryByAdapter(counters.Read(gpuMemory)).TryGetValue(gpu.LuidKey, out var used))
            {
                gpuUsed = (ulong)Math.Max(0, used);
            }

            gpuCelsius = GraphicsAdapters.TryGetTemperature(gpu);
        }

        return new SystemSnapshot(
            cpuPercent,
            Temperatures.HottestZoneCelsius(counters.Read(thermal)),
            SystemMemory.TryGetStatus(out var memory) ? memory : null,
            gpuPercent,
            gpuUsed,
            gpuCelsius);
    }

    /// <summary>Modelos: CPU e placa-mãe pelo registro (o que o Windows leu do firmware), GPU pelo DXGI.</summary>
    private HardwareInfo ReadHardware()
    {
        var cpu = ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
        var boardMaker = ReadRegistry(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardManufacturer");
        var boardModel = ReadRegistry(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct");
        var board = string.Join(' ', new[] { boardMaker, boardModel }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // Com placa integrada e dedicada (notebooks), a principal é a com mais memória dedicada.
        var gpu = GraphicsAdapters.Enumerate(_logger).MaxBy(a => a.DedicatedBytes);
        return new HardwareInfo(cpu, board.Length > 0 ? board : null, gpu);
    }

    private string? ReadRegistry(string path, string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return (key?.GetValue(name) as string)?.Trim();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Não foi possível ler {Path}\\{Name}", path, name);
            return null;
        }
    }

    private void Post(Action action, CancellationToken cancellation) => _dispatcher.TryEnqueue(() =>
    {
        if (!_disposed && !cancellation.IsCancellationRequested)
        {
            action();
        }
    });
}

