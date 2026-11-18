using Gearbox.Modules.Notch.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Gearbox.Modules.Notch.Views.Controls;

public sealed partial class BoltMascot : Grid
{
    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
        nameof(Mood), typeof(BoltMood), typeof(BoltMascot), new PropertyMetadata(BoltMood.Idle, (d, _) => ((BoltMascot)d).Apply()));

    public static readonly DependencyProperty IsAnimatedProperty = DependencyProperty.Register(
        nameof(IsAnimated), typeof(bool), typeof(BoltMascot), new PropertyMetadata(false, (d, _) => ((BoltMascot)d).Apply()));

    public static readonly DependencyProperty BodyBrushProperty = DependencyProperty.Register(
        nameof(BodyBrush), typeof(Brush), typeof(BoltMascot), new PropertyMetadata(null, (d, _) => ((BoltMascot)d).ApplyBrushes()));

    public static readonly DependencyProperty FaceBrushProperty = DependencyProperty.Register(
        nameof(FaceBrush), typeof(Brush), typeof(BoltMascot), new PropertyMetadata(null, (d, _) => ((BoltMascot)d).ApplyBrushes()));

    private const double CanvasSize = 32;
    private const double LineThickness = 1.1;

    private readonly TranslateTransform _bob = new();
    private readonly RotateTransform _leftEarTurn = new() { CenterX = 9.5, CenterY = 11.5 };
    private readonly RotateTransform _rightEarTurn = new() { CenterX = 22.5, CenterY = 11.5 };
    private readonly CompositeTransform _eyesTransform = new() { CenterX = 16, CenterY = 18.5 };
    private readonly ScaleTransform _mouthOpenScale = new() { CenterX = 16, CenterY = 24.6 };
    private readonly TranslateTransform _zzzMove = new();

    private readonly Ellipse _face = new() { Width = 24, Height = 21 };
    private readonly Polygon _leftEar = Triangle([new(5.5, 14), new(7, 3.5), new(14, 9.2)]);
    private readonly Polygon _rightEar = Triangle([new(26.5, 14), new(25, 3.5), new(18, 9.2)]);
    private readonly Canvas _eyesOpen = new();
    private readonly Ellipse _leftEye = new() { Width = 3, Height = 3.8 };
    private readonly Ellipse _rightEye = new() { Width = 3, Height = 3.8 };
    private readonly Path _eyesClosed = Line("M9.6,18.8 Q11.5,20.4 13.4,18.8 M18.6,18.8 Q20.5,20.4 22.4,18.8");
    private readonly Path _eyesHappy = Line("M9.8,19.8 L11.5,17.8 L13.2,19.8 M18.8,19.8 L20.5,17.8 L22.2,19.8");
    private readonly Polygon _nose = Triangle([new(15.1, 22), new(16.9, 22), new(16, 23.1)]);
    private readonly Path _mouth = Line("M13.9,24 Q15,25.4 16,24 Q17,25.4 18.1,24");
    private readonly Ellipse _mouthOpen = new() { Width = 2.4, Height = 2.2 };
    private readonly Path _whiskers = Line("M8.6,22.4 L3.4,21.4 M8.6,23.9 L3.4,24.6 M23.4,22.4 L28.6,21.4 M23.4,23.9 L28.6,24.6", 0.7);
    private readonly TextBlock _zzz = new() { Text = "z", FontSize = 7, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };

    private Storyboard? _story;

    public BoltMascot()
    {
        var canvas = new Canvas { Width = CanvasSize, Height = CanvasSize, RenderTransform = _bob };
        Place(_face, 4, 8);
        _leftEar.RenderTransform = _leftEarTurn;
        _rightEar.RenderTransform = _rightEarTurn;
        Place(_leftEye, 10, 16.6);
        Place(_rightEye, 19, 16.6);
        _eyesOpen.Children.Add(_leftEye);
        _eyesOpen.Children.Add(_rightEye);
        _eyesOpen.RenderTransform = _eyesTransform;
        Place(_mouthOpen, 14.8, 23.6);
        _mouthOpen.RenderTransform = _mouthOpenScale;
        Place(_zzz, 24.5, 0);
        _zzz.RenderTransform = _zzzMove;

        foreach (var part in new UIElement[] { _leftEar, _rightEar, _face, _whiskers, _eyesOpen, _eyesClosed, _eyesHappy, _nose, _mouth, _mouthOpen, _zzz })
        {
            canvas.Children.Add(part);
        }

        Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = canvas });
        Loaded += (_, _) => Apply();
        Unloaded += (_, _) => Stop();
        ApplyBrushes();
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

    public Brush? BodyBrush
    {
        get => (Brush?)GetValue(BodyBrushProperty);
        set => SetValue(BodyBrushProperty, value);
    }

    public Brush? FaceBrush
    {
        get => (Brush?)GetValue(FaceBrushProperty);
        set => SetValue(FaceBrushProperty, value);
    }

    private void ApplyBrushes()
    {
        _face.Fill = _leftEar.Fill = _rightEar.Fill = BodyBrush;
        _zzz.Foreground = BodyBrush;
        _leftEye.Fill = _rightEye.Fill = _nose.Fill = _mouthOpen.Fill = FaceBrush;
        _eyesClosed.Stroke = _eyesHappy.Stroke = _mouth.Stroke = _whiskers.Stroke = FaceBrush;
    }

    private void Apply()
    {
        Stop();
        var mood = Mood;
        var closed = mood is BoltMood.Sleeping or BoltMood.Yawn;
        var happy = mood is BoltMood.Happy or BoltMood.Wave;
        _eyesOpen.Visibility = closed || happy ? Visibility.Collapsed : Visibility.Visible;
        _eyesClosed.Visibility = closed ? Visibility.Visible : Visibility.Collapsed;
        _eyesHappy.Visibility = happy ? Visibility.Visible : Visibility.Collapsed;
        _mouthOpen.Visibility = mood is BoltMood.Talking or BoltMood.Yawn ? Visibility.Visible : Visibility.Collapsed;
        _mouth.Visibility = _mouthOpen.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        _zzz.Visibility = mood == BoltMood.Sleeping ? Visibility.Visible : Visibility.Collapsed;
        _eyesTransform.TranslateX = mood == BoltMood.Thinking ? 1.2 : 0;
        _eyesTransform.TranslateY = mood == BoltMood.Thinking ? -1 : 0;

        if (!IsAnimated || !IsLoaded)
        {
            return;
        }

        var story = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        switch (mood)
        {
            case BoltMood.Idle:
                Blink(story, 4);
                break;
            case BoltMood.Thinking:
                Blink(story, 3);
                Keys(story, _bob, "Y", 1.2, (0, 0), (0.6, -0.7), (1.2, 0));
                break;
            case BoltMood.Talking:
                Blink(story, 3);
                Keys(story, _mouthOpenScale, "ScaleY", 0.6, (0, 0.4), (0.15, 1.1), (0.3, 0.5), (0.45, 1), (0.6, 0.4));
                break;
            case BoltMood.Happy:
                Keys(story, _bob, "Y", 1.6, (0, 0), (0.2, -1.6), (0.4, 0), (0.6, -1.2), (0.8, 0), (1.6, 0));
                break;
            case BoltMood.Wave:
                Keys(story, _leftEarTurn, "Angle", 1.4, (0, 0), (0.15, -14), (0.3, 0), (0.7, 0), (1.4, 0));
                Keys(story, _rightEarTurn, "Angle", 1.4, (0, 0), (0.7, 0), (0.85, 14), (1, 0), (1.4, 0));
                Keys(story, _bob, "Y", 1.4, (0, 0), (0.35, -1), (0.7, 0), (1.05, -1), (1.4, 0));
                break;
            case BoltMood.EarTwitch:
                Blink(story, 2.4);
                Keys(story, _leftEarTurn, "Angle", 2.4, (0, 0), (1.2, 0), (1.3, -16), (1.4, 0), (1.5, -12), (1.6, 0), (2.4, 0));
                break;
            case BoltMood.Yawn:
                Keys(story, _mouthOpenScale, "ScaleY", 3, (0, 0.2), (0.8, 1.8), (1.8, 1.8), (2.4, 0.2), (3, 0.2));
                Keys(story, _mouthOpenScale, "ScaleX", 3, (0, 0.6), (0.8, 1.3), (1.8, 1.3), (2.4, 0.6), (3, 0.6));
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
        _leftEarTurn.Angle = _rightEarTurn.Angle = 0;
        _eyesTransform.ScaleY = 1;
        _mouthOpenScale.ScaleX = _mouthOpenScale.ScaleY = 1;
        _zzzMove.Y = 0;
        _zzz.Opacity = 1;
    }

    private void Blink(Storyboard story, double period) =>
        Keys(story, _eyesTransform, "ScaleY", period, (0, 1), (period - 0.2, 1), (period - 0.12, 0.1), (period - 0.04, 1), (period, 1));

    private static void Keys(Storyboard story, DependencyObject target, string property, double period, params (double At, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(period) };
        foreach (var (at, value) in frames)
        {
            animation.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(at), Value = value });
        }

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        story.Children.Add(animation);
    }

    private static void Place(UIElement element, double left, double top)
    {
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(element, left);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(element, top);
    }

    private static Polygon Triangle(Point[] points)
    {
        var polygon = new Polygon();
        foreach (var point in points)
        {
            polygon.Points.Add(point);
        }

        return polygon;
    }

    private static Path Line(string data, double thickness = LineThickness) => new()
    {
        Data = (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), data),
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
    };
}
