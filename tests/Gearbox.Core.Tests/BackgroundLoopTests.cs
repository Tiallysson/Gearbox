using Gearbox.Core.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gearbox.Core.Tests;

public sealed class BackgroundLoopTests
{
    [Fact]
    public async Task Religamento_AguardaLimpezaAnterior_EUsaOutroToken()
    {
        var entered = Signal();
        var release = Signal();
        var restarted = Signal();
        var running = 0;
        var calls = 0;
        var overlap = false;
        CancellationToken firstToken = default;
        CancellationToken secondToken = default;
        using var loop = new BackgroundLoop(async token =>
        {
            overlap |= Interlocked.Increment(ref running) > 1;
            try
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstToken = token;
                    entered.SetResult();
                    // Simula uma chamada nativa que ainda não observou o cancelamento.
                    await release.Task;
                }
                else
                {
                    secondToken = token;
                    restarted.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }, NullLogger.Instance);

        try
        {
            loop.SetActive(true);
            await Wait(entered.Task);
            loop.SetActive(false);
            loop.SetActive(true);
            Assert.True(firstToken.IsCancellationRequested);
            release.TrySetResult();
            await Wait(restarted.Task);
            Assert.False(overlap);
            Assert.NotEqual(firstToken, secondToken);
            Assert.False(secondToken.IsCancellationRequested);
        }
        finally
        {
            release.TrySetResult();
            loop.Dispose();
            await Wait(loop.Completion);
        }

        Assert.Equal(0, running);
    }

    [Fact]
    public async Task InicioPendenteCancelado_NaoExecutaNemCapturaTokenDeOutraGeracao()
    {
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        using var loop = new BackgroundLoop(async _ =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task;
        }, NullLogger.Instance);

        try
        {
            loop.SetActive(true);
            await Wait(entered.Task);
            loop.SetActive(false);
            loop.SetActive(true);
            loop.SetActive(false);
            release.TrySetResult();
            await Wait(loop.Completion);
            Assert.Equal(1, calls);
        }
        finally
        {
            release.TrySetResult();
            loop.Dispose();
            await Wait(loop.Completion);
        }
    }

    [Fact]
    public async Task FalhaObservada_PermiteNovaExecucao()
    {
        var calls = 0;
        using var loop = new BackgroundLoop(_ =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("sensor indisponível");
        }, NullLogger.Instance);

        loop.SetActive(true);
        await Wait(loop.Completion);
        loop.SetActive(true);
        await Wait(loop.Completion);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task DepoisDeDispose_NaoInicia()
    {
        var calls = 0;
        var loop = new BackgroundLoop(_ =>
        {
            calls++;
            return Task.CompletedTask;
        }, NullLogger.Instance);

        loop.Dispose();
        loop.SetActive(true);
        await Wait(loop.Completion);
        Assert.Equal(0, calls);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
}
