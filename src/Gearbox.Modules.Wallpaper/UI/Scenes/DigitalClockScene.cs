using System.Globalization;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Wallpaper.UI.Scenes;

internal sealed class DigitalClockScene : WallpaperScene
{
    private readonly DispatcherQueueTimer? _timer;
    private TextBlock? _time;
    private TextBlock? _date;
    private Visual? _seconds;
    private ScalarKeyFrameAnimation? _secondsAnimation;

    public DigitalClockScene()
    {
        _timer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_timer is not null)
        {
            _timer.IsRepeating = false;
            _timer.Tick += (_, _) => Tick();
        }
    }

    protected override void Build(Vector2 size)
    {
        AddSprite(size, Vector3.Zero, Vertical((0, Argb(255, 0x0C0D11)), (1, Argb(255, 0x050608))));
        var span = Math.Max(size.X, size.Y);
        var glow = AddSprite(new Vector2(span * 0.9f), new Vector3((size.X - (span * 0.9f)) / 2, (size.Y - (span * 0.9f)) / 2, 0), Glow(Argb(46, 0x4D6BFF), Argb(16, 0x7A4DFF), 0.4));
        var origin = glow.Offset;
        Animate(glow, "Offset", Loop(TimeSpan.FromSeconds(48), true, origin, origin + new Vector3(span * 0.06f, -span * 0.03f, 0), origin + new Vector3(-span * 0.05f, span * 0.04f, 0)));
        Animate(glow, "Opacity", Loop(TimeSpan.FromSeconds(16), true, 0.7f, 1f));

        var unit = Math.Min(size.X, size.Y * 1.6f);
        _time = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontWeight = FontWeights.ExtraLight,
            FontSize = Math.Max(10, unit * 0.15),
            CharacterSpacing = -20,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(240, 0xF4, 0xF6, 0xFB)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _date = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = Math.Max(6, unit * 0.022),
            CharacterSpacing = 160,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(150, 0xDD, 0xE3, 0xF2)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var line = new Border
        {
            Height = Math.Max(1, unit * 0.0016),
            Width = unit * 0.18,
            Margin = new Thickness(0, unit * 0.03, 0, 0),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(70, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var progress = new Border
        {
            Height = line.Height,
            Width = line.Width,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        line.Child = progress;
        _seconds = ElementCompositionPreview.GetElementVisual(progress);
        _seconds.CenterPoint = Vector3.Zero;
        _seconds.Scale = new Vector3(SecondsFraction(DateTime.Now), 1, 1);

        Overlay.Children.Add(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, size.Y * 0.04),
            Children = { _time, _date, line },
        });
        UpdateText();
        if (IsAnimating)
        {
            Tick();
        }
    }

    protected override void OnAnimatingChanged(bool animating)
    {
        if (animating)
        {
            Tick();
        }
        else
        {
            _timer?.Stop();
            _seconds?.StopAnimation("Scale.X");
            _secondsAnimation?.Dispose();
            _secondsAnimation = null;
        }
    }

    private void Tick()
    {
        UpdateText();
        if (!IsAnimating || _timer is null)
        {
            return;
        }

        var now = DateTime.Now;
        var remaining = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
        _timer.Interval = remaining + TimeSpan.FromMilliseconds(30);
        _timer.Start();
        StartSeconds(now, remaining);
    }

    private void StartSeconds(DateTime now, TimeSpan remaining)
    {
        if (_seconds is null || remaining <= TimeSpan.Zero)
        {
            return;
        }

        var animation = Compositor.CreateScalarKeyFrameAnimation();
        var linear = Compositor.CreateLinearEasingFunction();
        animation.InsertKeyFrame(0, SecondsFraction(now), linear);
        animation.InsertKeyFrame(1, 1, linear);
        animation.Duration = remaining;
        _seconds.StartAnimation("Scale.X", animation);
        _secondsAnimation?.Dispose();
        linear.Dispose();
        _secondsAnimation = animation;
    }

    private void UpdateText()
    {
        var now = DateTime.Now;
        if (_time is not null)
        {
            _time.Text = now.ToString("t", CultureInfo.CurrentCulture);
        }

        if (_date is not null)
        {
            _date.Text = now.ToString("dddd, d MMMM", CultureInfo.CurrentCulture).ToUpper(CultureInfo.CurrentCulture);
        }
    }

    private static float SecondsFraction(DateTime now) => (float)((now.Second + (now.Millisecond / 1000.0)) / 60.0);
}
