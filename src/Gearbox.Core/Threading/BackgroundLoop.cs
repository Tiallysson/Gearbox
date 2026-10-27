using Microsoft.Extensions.Logging;

namespace Gearbox.Core.Threading;

/// <summary>Executa no máximo uma amostragem por vez; cancelar não bloqueia a thread de UI.</summary>
public sealed class BackgroundLoop(Func<CancellationToken, Task> run, ILogger logger) : IDisposable
{
    private readonly Lock _gate = new();
    private CancellationTokenSource? _cancellation;
    private Task _completion = Task.CompletedTask;
    private bool _disposed;

    /// <summary>Inclui a execução atual e a limpeza das anteriores.</summary>
    public Task Completion
    {
        get
        {
            lock (_gate)
            {
                return _completion;
            }
        }
    }

    public void SetActive(bool active)
    {
        lock (_gate)
        {
            if (_disposed || active == (_cancellation is not null))
            {
                return;
            }

            if (!active)
            {
                _cancellation!.Cancel();
                _cancellation = null;
                return;
            }

            var source = new CancellationTokenSource();
            var token = source.Token;
            var previous = _completion;
            _cancellation = source;
            _completion = Task.Run(async () =>
            {
                try
                {
                    // Uma chamada nativa em andamento pode demorar a observar o cancelamento.
                    await previous.ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    await run(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Execução em segundo plano interrompida");
                }
                finally
                {
                    lock (_gate)
                    {
                        if (ReferenceEquals(_cancellation, source))
                        {
                            _cancellation = null;
                        }

                        source.Dispose();
                    }
                }
            });
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellation?.Cancel();
            _cancellation = null;
        }
    }
}
