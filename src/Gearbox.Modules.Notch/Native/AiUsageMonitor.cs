using Gearbox.Core.Threading;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Gearbox.Modules.Notch.Native;

internal sealed class AiUsageMonitor : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly AiProvider[] s_providers = [AiProvider.Anthropic, AiProvider.OpenAi];

    private readonly DispatcherQueue _dispatcher;
    private readonly ILogger _logger;
    private readonly BackgroundLoop _loop;
    private readonly AiUsageClient _client = new();
    private volatile AiUsageSettings _settings;
    private bool _disposed;

    public AiUsageMonitor(DispatcherQueue dispatcher, AiUsageSettings settings, ILogger logger)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _logger = logger;
        _loop = new BackgroundLoop(RunAsync, logger);
        _loop.SetActive(true);
    }

    public event EventHandler<List<AiUsage>>? Changed;

    public void Configure(AiUsageSettings settings)
    {
        _settings = settings;
        Refresh();
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        _loop.SetActive(false);
        _loop.SetActive(true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _loop.Dispose();
        _ = _loop.Completion.ContinueWith(_ => _client.Dispose(), TaskScheduler.Default);
    }

    private async Task RunAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            var usages = await SampleAsync(_settings, cancellation).ConfigureAwait(false);
            Post(usages, cancellation);
        }
        while (await timer.WaitForNextTickAsync(cancellation).ConfigureAwait(false));
    }

    private async Task<List<AiUsage>> SampleAsync(AiUsageSettings settings, CancellationToken cancellation)
    {
        var monthStart = BillingPeriod.MonthStart(DateTimeOffset.UtcNow);
        var usages = new List<AiUsage>();
        foreach (var provider in s_providers)
        {
            var budget = settings.Budget(provider);
            if (budget <= 0 || AiKeyVault.Get(provider) is not { } key)
            {
                continue;
            }

            try
            {
                var (status, spent) = await _client.FetchMonthToDateAsync(provider, key, monthStart, cancellation).ConfigureAwait(false);
                usages.Add(new AiUsage(provider, status, spent, budget));
            }
            catch (Exception ex) when (!cancellation.IsCancellationRequested && AiUsageClient.IsTransient(ex))
            {
                _logger.LogWarning(ex, "Não foi possível ler o uso de {Provider}", provider);
                usages.Add(new AiUsage(provider, AiUsageStatus.Unavailable, 0m, budget));
            }
        }

        return usages;
    }

    private void Post(List<AiUsage> usages, CancellationToken cancellation) => _dispatcher.TryEnqueue(() =>
    {
        if (!_disposed && !cancellation.IsCancellationRequested)
        {
            Changed?.Invoke(this, usages);
        }
    });
}
