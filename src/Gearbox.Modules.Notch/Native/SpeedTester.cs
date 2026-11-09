using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Native;

internal enum SpeedTestPhase
{
    Ping,
    Download,
    Upload,
    Done,
}

internal readonly record struct SpeedTestProgress(
    SpeedTestPhase Phase,
    double? PingMilliseconds,
    double? DownBitsPerSecond,
    double? UpBitsPerSecond);

internal sealed class SpeedTester : IDisposable
{
    private static readonly Uri Server = new("https://speed.cloudflare.com/");
    private static readonly TimeSpan PhaseDuration = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan PingPhaseTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(250);
    private const int Streams = 4;
    private const int PingRounds = 6;
    private const int DownloadChunkBytes = 25_000_000;
    private const int UploadChunkBytes = 4_000_000;
    private const long MaxDownloadBytes = 300_000_000;
    private const long MaxUploadBytes = 100_000_000;
    private const int UploadPieceBytes = 64 * 1024;

    private readonly HttpClient _http;

    public SpeedTester()
    {
        _http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = Streams, AutomaticDecompression = DecompressionMethods.None })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Gearbox");
    }

    public async Task<SpeedTestProgress> RunAsync(IProgress<SpeedTestProgress> progress, CancellationToken cancellation)
    {
        progress.Report(new SpeedTestProgress(SpeedTestPhase.Ping, null, null, null));
        var ping = await MeasurePingAsync(cancellation);

        progress.Report(new SpeedTestProgress(SpeedTestPhase.Download, ping, null, null));
        var down = await MeasureAsync(
            (counter, token) => DownloadAsync(counter, token),
            MaxDownloadBytes,
            bits => progress.Report(new SpeedTestProgress(SpeedTestPhase.Download, ping, bits, null)),
            cancellation);

        progress.Report(new SpeedTestProgress(SpeedTestPhase.Upload, ping, down, null));
        var up = await MeasureAsync(
            (counter, token) => UploadAsync(counter, token),
            MaxUploadBytes,
            bits => progress.Report(new SpeedTestProgress(SpeedTestPhase.Upload, ping, down, bits)),
            cancellation);

        var result = new SpeedTestProgress(SpeedTestPhase.Done, ping, down, up);
        progress.Report(result);
        return result;
    }

    public void Dispose() => _http.Dispose();

    private async Task<double?> MeasurePingAsync(CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(PingPhaseTimeout);
        var samples = new List<double>(PingRounds);
        for (var round = 0; round < PingRounds; round++)
        {
            var clock = Stopwatch.StartNew();
            using var response = await _http.GetAsync(new Uri(Server, "__down?bytes=0"), HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            response.EnsureSuccessStatusCode();
            if (round > 0)
            {
                samples.Add(clock.Elapsed.TotalMilliseconds);
            }
        }

        return SpeedMath.Median(samples);
    }

    private static async Task<double> MeasureAsync(
        Func<ByteCounter, CancellationToken, Task> worker,
        long maxBytes,
        Action<double> report,
        CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(PhaseDuration);
        var counter = new ByteCounter(maxBytes, deadline);
        var workers = Enumerable.Range(0, Streams).Select(_ => RunWorkerAsync(worker, counter, deadline)).ToList();
        var all = Task.WhenAll(workers);
        while (!all.IsCompleted)
        {
            await Task.WhenAny(all, Task.Delay(ReportInterval, CancellationToken.None));
            report(counter.BitsPerSecond());
        }

        await all;
        counter.Seal();
        cancellation.ThrowIfCancellationRequested();
        if (counter.Total == 0)
        {
            throw new HttpRequestException("O teste de velocidade não transferiu nenhum dado.");
        }

        return counter.BitsPerSecond();
    }

    private static async Task RunWorkerAsync(Func<ByteCounter, CancellationToken, Task> worker, ByteCounter counter, CancellationTokenSource deadline)
    {
        var token = deadline.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await worker(counter, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex) when ((ex is HttpRequestException or IOException) && token.IsCancellationRequested)
        {
        }
        catch
        {
            counter.Stop();
            throw;
        }
    }

    private async Task DownloadAsync(ByteCounter counter, CancellationToken token)
    {
        using var response = await _http.GetAsync(new Uri(Server, "__down?bytes=" + DownloadChunkBytes), HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            counter.Add(read);
        }
    }

    private async Task UploadAsync(ByteCounter counter, CancellationToken token)
    {
        using var content = new CountingContent(UploadChunkBytes, counter);
        using var response = await _http.PostAsync(new Uri(Server, "__up"), content, token);
        response.EnsureSuccessStatusCode();
    }

    private sealed class ByteCounter(long maxBytes, CancellationTokenSource deadline)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _total;
        private long _firstByteTicks = -1;
        private int _stopped;
        private int _sealed;

        public long Total => Interlocked.Read(ref _total);

        public double BitsPerSecond()
        {
            var first = Interlocked.Read(ref _firstByteTicks);
            return first < 0 ? 0 : SpeedMath.BitsPerSecond(Total, _clock.Elapsed - TimeSpan.FromTicks(first));
        }

        public void Add(int bytes)
        {
            Interlocked.CompareExchange(ref _firstByteTicks, _clock.Elapsed.Ticks, -1);
            if (Interlocked.Add(ref _total, bytes) >= maxBytes)
            {
                Stop();
            }
        }

        public void Stop()
        {
            if (Volatile.Read(ref _sealed) == 1 || Interlocked.Exchange(ref _stopped, 1) == 1)
            {
                return;
            }

            try
            {
                deadline.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Seal()
        {
            Volatile.Write(ref _sealed, 1);
            _clock.Stop();
        }
    }

    private sealed class CountingContent(int length, ByteCounter counter) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            var piece = new byte[UploadPieceBytes];
            RandomNumberGenerator.Fill(piece);
            var remaining = length;
            while (remaining > 0)
            {
                var size = Math.Min(piece.Length, remaining);
                await stream.WriteAsync(piece.AsMemory(0, size), cancellationToken);
                counter.Add(size);
                remaining -= size;
            }
        }

        protected override bool TryComputeLength(out long computed)
        {
            computed = length;
            return true;
        }
    }
}
