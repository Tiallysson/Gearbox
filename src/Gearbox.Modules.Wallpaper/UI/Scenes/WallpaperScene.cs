using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Gearbox.Modules.Wallpaper.UI.Scenes;

internal abstract class WallpaperScene : IDisposable
{
    private readonly Grid _backdrop;
    private readonly List<(CompositionObject Target, string Property, CompositionAnimation Animation)> _animations = [];
    private readonly HashSet<IDisposable> _owned = new(ReferenceEqualityComparer.Instance);
    private Vector2 _size;
    private bool _animating;
    private bool _started;
    private bool _disposed;

    protected WallpaperScene()
    {
        Root = new Grid { Background = new SolidColorBrush(Colors.Black), IsHitTestVisible = false };
        _backdrop = new Grid();
        Overlay = new Grid();
        Root.Children.Add(_backdrop);
        Root.Children.Add(Overlay);
        Compositor = ElementCompositionPreview.GetElementVisual(Root).Compositor;
        Container = Compositor.CreateContainerVisual();
        ElementCompositionPreview.SetElementChildVisual(_backdrop, Container);
        Root.SizeChanged += OnSizeChanged;
    }

    public Grid Root { get; }

    protected Grid Overlay { get; }

    protected Compositor Compositor { get; }

    protected ContainerVisual Container { get; }

    protected bool IsAnimating => _animating;

    protected bool IsDisposed => _disposed;

    public void SetAnimating(bool animating)
    {
        if (_disposed || animating == _animating)
        {
            return;
        }

        _animating = animating;
        if (animating)
        {
            StartOrResume();
        }
        else
        {
            foreach (var (target, property, _) in _animations)
            {
                target.TryGetAnimationController(property)?.Pause();
            }
        }

        OnAnimatingChanged(animating);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Root.SizeChanged -= OnSizeChanged;
        OnAnimatingChanged(false);
        StopAll();
        Container.Children.RemoveAll();
        Overlay.Children.Clear();
        DisposeOwned();
        ElementCompositionPreview.SetElementChildVisual(_backdrop, null);
        Container.Dispose();
    }

    protected abstract void Build(Vector2 size);

    protected virtual void OnAnimatingChanged(bool animating)
    {
    }

    protected T Own<T>(T disposable)
        where T : IDisposable
    {
        _owned.Add(disposable);
        return disposable;
    }

    protected void Animate(CompositionObject target, string property, CompositionAnimation animation)
    {
        Own(animation);
        _animations.Add((target, property, animation));
        if (_started)
        {
            target.StartAnimation(property, animation);
            if (!_animating)
            {
                target.TryGetAnimationController(property)?.Pause();
            }
        }
    }

    protected SpriteVisual AddSprite(Vector2 size, Vector3 offset, CompositionBrush brush, ContainerVisual? parent = null)
    {
        var sprite = Own(Compositor.CreateSpriteVisual());
        Own(brush);
        sprite.Size = size;
        sprite.Offset = offset;
        sprite.Brush = brush;
        (parent ?? Container).Children.InsertAtTop(sprite);
        return sprite;
    }

    protected CompositionRadialGradientBrush Glow(Color center, Color middle, double middleAt = 0.45)
    {
        var brush = Own(Compositor.CreateRadialGradientBrush());
        brush.EllipseCenter = new Vector2(0.5f);
        brush.EllipseRadius = new Vector2(0.5f);
        brush.ColorStops.Add(Stop(0, center));
        brush.ColorStops.Add(Stop((float)middleAt, middle));
        brush.ColorStops.Add(Stop(1, Color.FromArgb(0, middle.R, middle.G, middle.B)));
        return brush;
    }

    protected CompositionLinearGradientBrush Vertical(params (float At, Color Color)[] stops)
    {
        var brush = Own(Compositor.CreateLinearGradientBrush());
        brush.StartPoint = new Vector2(0.5f, 0);
        brush.EndPoint = new Vector2(0.5f, 1);
        foreach (var (at, color) in stops)
        {
            brush.ColorStops.Add(Stop(at, color));
        }

        return brush;
    }

    protected CompositionColorGradientStop Stop(float offset, Color color) => Own(Compositor.CreateColorGradientStop(offset, color));

    protected CompositionEasingFunction EaseInOut() =>
        Own(Compositor.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0), new Vector2(0.55f, 1)));

    protected CompositionEasingFunction Linear() => Own(Compositor.CreateLinearEasingFunction());

    protected ScalarKeyFrameAnimation Loop(TimeSpan duration, bool alternate, params float[] values)
    {
        var animation = Own(Compositor.CreateScalarKeyFrameAnimation());
        var ease = EaseInOut();
        for (var i = 0; i < values.Length; i++)
        {
            animation.InsertKeyFrame(values.Length == 1 ? 1 : i / (float)(values.Length - 1), values[i], ease);
        }

        return Configure(animation, duration, alternate);
    }

    protected Vector3KeyFrameAnimation Loop(TimeSpan duration, bool alternate, params Vector3[] values)
    {
        var animation = Own(Compositor.CreateVector3KeyFrameAnimation());
        var ease = EaseInOut();
        for (var i = 0; i < values.Length; i++)
        {
            animation.InsertKeyFrame(values.Length == 1 ? 1 : i / (float)(values.Length - 1), values[i], ease);
        }

        return Configure(animation, duration, alternate);
    }

    protected Vector2KeyFrameAnimation Loop(TimeSpan duration, bool alternate, params Vector2[] values)
    {
        var animation = Own(Compositor.CreateVector2KeyFrameAnimation());
        var ease = EaseInOut();
        for (var i = 0; i < values.Length; i++)
        {
            animation.InsertKeyFrame(values.Length == 1 ? 1 : i / (float)(values.Length - 1), values[i], ease);
        }

        return Configure(animation, duration, alternate);
    }

    protected ColorKeyFrameAnimation Loop(TimeSpan duration, params Color[] values)
    {
        var animation = Own(Compositor.CreateColorKeyFrameAnimation());
        var ease = EaseInOut();
        for (var i = 0; i < values.Length; i++)
        {
            animation.InsertKeyFrame(i / (float)(values.Length - 1), values[i], ease);
        }

        animation.InterpolationColorSpace = CompositionColorSpace.Rgb;
        return Configure(animation, duration, alternate: false);
    }

    protected static Color Argb(byte a, uint rgb) => Color.FromArgb(a, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static T Configure<T>(T animation, TimeSpan duration, bool alternate)
        where T : KeyFrameAnimation
    {
        animation.Duration = duration;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;
        animation.Direction = alternate ? AnimationDirection.Alternate : AnimationDirection.Normal;
        return animation;
    }

    private void StartOrResume()
    {
        if (!_started)
        {
            _started = true;
            foreach (var (target, property, animation) in _animations)
            {
                target.StartAnimation(property, animation);
            }

            return;
        }

        foreach (var (target, property, _) in _animations)
        {
            target.TryGetAnimationController(property)?.Resume();
        }
    }

    private void StopAll()
    {
        foreach (var (target, property, _) in _animations)
        {
            target.StopAnimation(property);
        }

        _animations.Clear();
    }

    private void DisposeOwned()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }

        _owned.Clear();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        if (size.X < 1 || size.Y < 1 || size == _size)
        {
            return;
        }

        _size = size;
        StopAll();
        Container.Children.RemoveAll();
        Overlay.Children.Clear();
        DisposeOwned();
        _started = false;
        Build(size);
        if (_animating)
        {
            StartOrResume();
        }
    }
}
