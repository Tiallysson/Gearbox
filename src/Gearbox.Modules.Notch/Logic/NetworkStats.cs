namespace Gearbox.Modules.Notch.Logic;

public readonly record struct NetworkCounters(long BytesReceived, long BytesSent, TimeSpan Timestamp);

public readonly record struct NetworkSample(double DownBitsPerSecond, double UpBitsPerSecond, double? PingMilliseconds);

public readonly record struct ChartPoint(double X, double Y);

public static class NetworkRates
{
    public static NetworkSample Between(NetworkCounters previous, NetworkCounters current, double? pingMilliseconds)
    {
        var seconds = (current.Timestamp - previous.Timestamp).TotalSeconds;
        if (seconds <= 0)
        {
            return new NetworkSample(0, 0, pingMilliseconds);
        }

        return new NetworkSample(
            Math.Max(0, current.BytesReceived - previous.BytesReceived) * 8 / seconds,
            Math.Max(0, current.BytesSent - previous.BytesSent) * 8 / seconds,
            pingMilliseconds);
    }

    public static string Format(double bitsPerSecond, IFormatProvider? culture = null) =>
        !double.IsFinite(bitsPerSecond) || bitsPerSecond < 1000
            ? "0 Kbps"
            : NetworkLinks.FormatSpeed((ulong)bitsPerSecond, culture);
}

public sealed class NetworkHistory
{
    public const int Capacity = 30;

    private readonly List<NetworkSample> _samples = [];

    public IReadOnlyList<NetworkSample> Samples => _samples;

    public NetworkSample? Latest => _samples.Count == 0 ? null : _samples[^1];

    public double? LatestPing => _samples.LastOrDefault(sample => sample.PingMilliseconds is not null).PingMilliseconds;

    public void Add(NetworkSample sample)
    {
        _samples.Add(sample);
        if (_samples.Count > Capacity)
        {
            _samples.RemoveAt(0);
        }
    }

    public void Clear() => _samples.Clear();
}

public static class NetworkChart
{
    public const double MinimumScale = 1_000_000;

    public static double Nice(double value)
    {
        if (!double.IsFinite(value) || value <= MinimumScale)
        {
            return MinimumScale;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in new[] { 1.0, 2.0, 5.0, 10.0 })
        {
            if (step * magnitude >= value)
            {
                return step * magnitude;
            }
        }

        return 10 * magnitude;
    }

    public static double ScaleFor(IEnumerable<NetworkSample> samples) =>
        Nice(samples.Select(sample => Math.Max(sample.DownBitsPerSecond, sample.UpBitsPerSecond)).DefaultIfEmpty(0).Max());

    public static List<ChartPoint> Points(IReadOnlyList<double> values, double scale, double width, double height)
    {
        var points = new List<ChartPoint>(values.Count);
        if (values.Count == 0 || scale <= 0 || width <= 0 || height <= 0)
        {
            return points;
        }

        var step = width / (NetworkHistory.Capacity - 1);
        var start = width - (step * (values.Count - 1));
        for (var i = 0; i < values.Count; i++)
        {
            var ratio = double.IsFinite(values[i]) ? Math.Clamp(values[i] / scale, 0, 1) : 0;
            points.Add(new ChartPoint(start + (step * i), height - (ratio * height)));
        }

        return points;
    }
}

public enum SpeedTestDecision
{
    Start,
    Cancel,
    ConfirmMetered,
}

public static class SpeedTestGate
{
    public static SpeedTestDecision Decide(bool running, bool metered, bool confirmationShown) =>
        running ? SpeedTestDecision.Cancel
        : metered && !confirmationShown ? SpeedTestDecision.ConfirmMetered
        : SpeedTestDecision.Start;
}

public static class SpeedMath
{
    public static double BitsPerSecond(long bytes, TimeSpan elapsed) =>
        elapsed <= TimeSpan.Zero || bytes <= 0 ? 0 : bytes * 8 / elapsed.TotalSeconds;

    public static double? Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
