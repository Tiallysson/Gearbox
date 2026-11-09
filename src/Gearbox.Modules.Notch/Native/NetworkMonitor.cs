using System.Diagnostics;
using System.Net.NetworkInformation;
using Gearbox.Core.Threading;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Gearbox.Modules.Notch.Native;

internal sealed class NetworkMonitor : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private const int PingEveryTicks = 2;
    private const int PingTimeoutMilliseconds = 1000;
    private const string PingHost = "1.1.1.1";

    private readonly DispatcherQueue _dispatcher;
    private readonly ILogger _logger;
    private readonly BackgroundLoop _loop;
    private bool _disposed;

    public NetworkMonitor(DispatcherQueue dispatcher, ILogger logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _loop = new BackgroundLoop(RunAsync, logger);
    }

    public event EventHandler<NetworkSample>? Sampled;

    public void SetActive(bool active) => _loop.SetActive(active);

    public void Dispose()
    {
        _disposed = true;
        _loop.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellation)
    {
        using var ping = new Ping();
        var clock = Stopwatch.StartNew();
        var previous = ReadCounters(clock);
        double? latency = await PingAsync(ping);
        var tick = 0;
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(cancellation))
        {
            tick++;
            if (tick % PingEveryTicks == 0)
            {
                latency = await PingAsync(ping);
            }

            var current = ReadCounters(clock);
            var sample = NetworkRates.Between(previous, current, latency);
            previous = current;
            _dispatcher.TryEnqueue(() =>
            {
                if (!_disposed && !cancellation.IsCancellationRequested)
                {
                    Sampled?.Invoke(this, sample);
                }
            });
        }
    }

    private NetworkCounters ReadCounters(Stopwatch clock)
    {
        long received = 0;
        long sent = 0;
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel ||
                    adapter.GetIPProperties().GatewayAddresses.Count == 0)
                {
                    continue;
                }

                var statistics = adapter.GetIPStatistics();
                received += statistics.BytesReceived;
                sent += statistics.BytesSent;
            }
        }
        catch (NetworkInformationException ex)
        {
            _logger.LogDebug(ex, "Não foi possível ler o tráfego de rede");
        }

        return new NetworkCounters(received, sent, clock.Elapsed);
    }

    private async Task<double?> PingAsync(Ping ping)
    {
        try
        {
            var reply = await ping.SendPingAsync(PingHost, PingTimeoutMilliseconds);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (Exception ex) when (ex is PingException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Ping para {Host} falhou", PingHost);
            return null;
        }
    }
}
