using System.Diagnostics;
using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Notch.UI;

internal readonly record struct SpringChannel(double From, double To, Action<double> Apply);

internal sealed class SpringAnimator
{
    private readonly Stopwatch _clock = new();
    private SpringChannel[] _channels = [];
    private SpringMotion _motion;
    private Action? _frame;
    private Action? _completed;
    private int _generation;

    public bool IsRunning { get; private set; }

    public void Start(SpringMotion motion, SpringChannel[] channels, Action? frame = null, Action? completed = null)
    {
        Stop();
        _generation++;
        _motion = motion;
        _channels = channels;
        _frame = frame;
        _completed = completed;
        IsRunning = true;
        _clock.Restart();
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _generation++;
        _clock.Stop();
        IsRunning = false;
        _channels = [];
        _frame = null;
        _completed = null;
    }

    private void OnRendering(object? sender, object e)
    {
        if (!IsRunning)
        {
            return;
        }

        var generation = _generation;
        var seconds = _clock.Elapsed.TotalSeconds;
        var settled = _motion.IsSettled(seconds);
        var completed = _completed;
        foreach (var channel in _channels)
        {
            channel.Apply(settled ? channel.To : _motion.Interpolate(channel.From, channel.To, seconds));
            if (generation != _generation)
            {
                return;
            }
        }

        _frame?.Invoke();
        if (!settled || generation != _generation)
        {
            return;
        }

        Stop();
        completed?.Invoke();
    }
}
