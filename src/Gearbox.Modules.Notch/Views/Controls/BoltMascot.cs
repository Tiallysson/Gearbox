using Gearbox.Modules.Notch.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Gearbox.Modules.Notch.Views.Controls;

public sealed partial class BoltMascot : Grid
{
    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
        nameof(Mood), typeof(BoltMood), typeof(BoltMascot), new PropertyMetadata(BoltMood.Idle, (d, _) => ((BoltMascot)d).Apply()));

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(BoltMascot), new PropertyMetadata(false, (d, _) => ((BoltMascot)d).Apply()));

    private const double CanvasSize = 32;
    private const double GlowOpacity = 0.25;
    private const double SleepingLightOpacity = 0.3;

    private static readonly Color s_shellColor = Color.FromArgb(0xFF, 0xEC, 0xEE, 0xF1);
    private static readonly Color s_shadeColor = Color.FromArgb(0xFF, 0xB4, 0xBC, 0xC6);
    private static readonly Color s_visorColor = Color.FromArgb(0xFF, 0x0B, 0x0F, 0x14);
    private static readonly Color s_visorEdgeColor = Color.FromArgb(0xFF, 0x2B, 0x35, 0x42);
    private static readonly Color s_ledColor = Color.FromArgb(0xFF, 0x5C, 0xE1, 0xFF);
    private static readonly Color s_highlightColor = Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF);

    private readonly SolidColorBrush _shell = new(s_shellColor);
    private readonly SolidColorBrush _shade = new(s_shadeColor);
    private readonly SolidColorBrush _led = new(s_ledColor);

    private readonly TranslateTransform _bob = new();
    private readonly RotateTransform _antennaTurn = new() { CenterX = 16, CenterY = 8.4 };
    private readonly CompositeTransform _eyesTransform = new() { CenterX = 16, CenterY = 17.1 };
    private readonly ScaleTransform _mouthScale = new() { CenterX = 16, CenterY = 21.5 };
    private readonly RotateTransform _handTurn = new() { CenterX = 28.7, CenterY = 25 };
    private readonly TranslateTransform _zzzMove = new();

    private readonly Canvas _light = new();
    private readonly Canvas _eyesOpen = new();
    private readonly Canvas _eyesHappy = new();
    private readonly Canvas _eyesClosed = new();
    private readonly Rectangle _mouth = new() { Width = 3.2, Height = 1.8, RadiusX = 0.9, RadiusY = 0.9 };
    private readonly Canvas _hand = new();
    private readonly TextBlock _zzz = new() { Text = "z", FontSize = 6, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };

    private Storyboard? _story;

    public BoltMascot()
    {
        var rig = new Canvas { Width = CanvasSize, Height = CanvasSize, RenderTransform = _bob };
        rig.Children.Add(BuildAntenna());
        rig.Children.Add(Rounded(14.2, 7.4, 3.6, 1.6, 0.8, _shade));
        rig.Children.Add(Rounded(2.4, 14, 3.2, 8, 1.6, _shade));
        rig.Children.Add(Rounded(26.4, 14, 3.2, 8, 1.6, _shade));
        rig.Children.Add(Rounded(4.5, 8.2, 23, 20, 9, _shell));
        rig.Children.Add(Line("M8.4,13.4 Q9.8,10 13.6,9.5", new SolidColorBrush(s_highlightColor), 1.1));
        var visor = Rounded(7.2, 11.6, 17.6, 12.6, 5, new SolidColorBrush(s_visorColor));
        visor.Stroke = new SolidColorBrush(s_visorEdgeColor);
        visor.StrokeThickness = 0.5;
        rig.Children.Add(visor);
        rig.Children.Add(BuildEyes());
        Place(_mouth, 14.4, 20.6);
        _mouth.Fill = _led;
        _mouth.RenderTransform = _mouthScale;
        rig.Children.Add(_mouth);
        rig.Children.Add(BuildHand());
        Place(_zzz, 23.6, -1.4);
        _zzz.Foreground = _shade;
        _zzz.RenderTransform = _zzzMove;
        rig.Children.Add(_zzz);

        Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = rig });
        Loaded += (_, _) => Apply();
        Unloaded += (_, _) => Stop();
        Apply();
    }

    public BoltMood Mood
    {
        get => (BoltMood)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }

    private Canvas BuildAntenna()
    {
        var antenna = new Canvas { RenderTransform = _antennaTurn };
        antenna.Children.Add(Line("M16,4.6 L16,8.4", _shade, 1.1));
        _light.Children.Add(Dot(16, 3.3, 2.6, _led, GlowOpacity));
        _light.Children.Add(Dot(16, 3.3, 1.7, _led, 1));
        antenna.Children.Add(_light);
        return antenna;
    }

    private Canvas BuildEyes()
    {
        foreach (var left in new[] { 10.4, 18.6 })
        {
            var glow = Rounded(left - 0.8, 14.2, 4.6, 5.8, 2.3, _led);
            glow.Opacity = GlowOpacity;
            _eyesOpen.Children.Add(glow);
            _eyesOpen.Children.Add(Rounded(left, 15, 3, 4.2, 1.5, _led));
        }

        AddGlowingStroke(_eyesHappy, "M10,19 Q12,15.4 14,19 M18.2,19 Q20.2,15.4 22.2,19");
        AddGlowingStroke(_eyesClosed, "M10.2,17.8 Q12,19.4 13.8,17.8 M18.4,17.8 Q20.2,19.4 22,17.8");

        var eyes = new Canvas { RenderTransform = _eyesTransform };
        eyes.Children.Add(_eyesOpen);
        eyes.Children.Add(_eyesHappy);
        eyes.Children.Add(_eyesClosed);
        return eyes;
    }

    private void AddGlowingStroke(Canvas target, string data)
    {
        var glow = Line(data, _led, 3);
        glow.Opacity = GlowOpacity;
        target.Children.Add(glow);
        target.Children.Add(Line(data, _led, 1.5));
    }

    private Canvas BuildHand()
    {
        _hand.RenderTransform = _handTurn;
        _hand.Children.Add(Rounded(27.6, 19.6, 3.6, 5.4, 1.8, _shell));
        _hand.Children.Add(Dot(27.7, 23.4, 0.9, _shell, 1));
        return _hand;
    }

    private void Apply()
    {
        Stop();
        var mood = Mood;
        var closed = mood is BoltMood.Sleeping or BoltMood.Yawn;
        var happy = mood is BoltMood.Happy or BoltMood.Wave;
        _eyesOpen.Visibility = Show(!closed && !happy);
        _eyesHappy.Visibility = Show(happy);
        _eyesClosed.Visibility = Show(closed);
        _mouth.Visibility = Show(mood is BoltMood.Talking or BoltMood.Yawn);
        _hand.Visibility = Show(mood == BoltMood.Wave);
        _zzz.Visibility = Show(mood == BoltMood.Sleeping);
        _light.Opacity = mood == BoltMood.Sleeping ? SleepingLightOpacity : 1;
        _eyesTransform.TranslateX = mood == BoltMood.Thinking ? 1.2 : 0;
        _eyesTransform.TranslateY = mood == BoltMood.Thinking ? -1 : 0;

        if (!IsAnimated || !IsLoaded)
        {
            return;
        }

        var story = new Storyboard();
        switch (mood)
        {
            case BoltMood.Idle:
                Blink(story, 4);
                break;
            case BoltMood.Thinking:
                Blink(story, 3);
                Keys(story, _bob, "Y", 1.2, (0, 0), (0.6, -0.7), (1.2, 0));
                Keys(story, _light, "Opacity", 0.9, (0, 1), (0.45, 0.35), (0.9, 1));
                break;
            case BoltMood.Talking:
                Blink(story, 3);
                Keys(story, _mouthScale, "ScaleY", 0.6, (0, 0.4), (0.15, 1.1), (0.3, 0.5), (0.45, 1), (0.6, 0.4));
                break;
            case BoltMood.Happy:
                Keys(story, _bob, "Y", 1.6, (0, 0), (0.2, -1.6), (0.4, 0), (0.6, -1.2), (0.8, 0), (1.6, 0));
                break;
            case BoltMood.Wave:
                Keys(story, _handTurn, "Angle", 1.4, (0, 0), (0.28, -22), (0.56, 8), (0.84, -18), (1.12, 4), (1.4, 0));
                Keys(story, _bob, "Y", 1.4, (0, 0), (0.35, -1), (0.7, 0), (1.05, -1), (1.4, 0));
                break;
            case BoltMood.AntennaTwitch:
                Blink(story, 2.4);
                Keys(story, _antennaTurn, "Angle", 2.4, (0, 0), (1.2, 0), (1.3, -16), (1.4, 0), (1.5, 12), (1.6, 0), (2.4, 0));
                break;
            case BoltMood.Yawn:
                Keys(story, _mouthScale, "ScaleY", 3, (0, 0.2), (0.8, 1.8), (1.8, 1.8), (2.4, 0.2), (3, 0.2));
                Keys(story, _mouthScale, "ScaleX", 3, (0, 0.6), (0.8, 1.3), (1.8, 1.3), (2.4, 0.6), (3, 0.6));
                break;
            case BoltMood.Sleeping:
                Keys(story, _zzzMove, "Y", 2.4, (0, 2), (2.4, -4));
                Keys(story, _zzz, "Opacity", 2.4, (0, 0), (0.6, 1), (1.8, 1), (2.4, 0));
                Keys(story, _bob, "Y", 2.4, (0, 0), (1.2, 0.5), (2.4, 0));
                break;
        }

        _story = story;
        story.Begin();
    }

    private void Stop()
    {
        _story?.Stop();
        _story = null;
        _bob.Y = 0;
        _antennaTurn.Angle = 0;
        _handTurn.Angle = 0;
        _eyesTransform.ScaleY = 1;
        _mouthScale.ScaleX = _mouthScale.ScaleY = 1;
        _zzzMove.Y = 0;
        _zzz.Opacity = 1;
    }

    private void Blink(Storyboard story, double period) =>
        Keys(story, _eyesTransform, "ScaleY", period, (0, 1), (period - 0.2, 1), (period - 0.12, 0.1), (period - 0.04, 1), (period, 1));

    private static void Keys(Storyboard story, DependencyObject target, string property, double period, params (double At, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(period),
            RepeatBehavior = RepeatBehavior.Forever,
        };
        foreach (var (at, value) in frames)
        {
            animation.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at), Value = value });
        }

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        story.Children.Add(animation);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static void Place(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
    }

    private static Rectangle Rounded(double left, double top, double width, double height, double radius, Brush fill)
    {
        var rectangle = new Rectangle { Width = width, Height = height, RadiusX = radius, RadiusY = radius, Fill = fill };
        Place(rectangle, left, top);
        return rectangle;
    }

    private static Ellipse Dot(double centerX, double centerY, double radius, Brush fill, double opacity)
    {
        var ellipse = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = fill, Opacity = opacity };
        Place(ellipse, centerX - radius, centerY - radius);
        return ellipse;
    }

    private static Path Line(string data, Brush stroke, double thickness) => new()
    {
        Data = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), data),
        Stroke = stroke,
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
    };
}
