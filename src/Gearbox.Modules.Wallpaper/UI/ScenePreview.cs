using Gearbox.Modules.Wallpaper.Logic.Gallery;
using Gearbox.Modules.Wallpaper.UI.Scenes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Modules.Wallpaper.UI;

public sealed partial class ScenePreview : ContentControl
{
    public static readonly DependencyProperty SceneIdProperty = DependencyProperty.Register(
        nameof(SceneId), typeof(string), typeof(ScenePreview), new PropertyMetadata(string.Empty, (d, _) => ((ScenePreview)d).Recreate()));

    public static readonly DependencyProperty IsAnimatingProperty = DependencyProperty.Register(
        nameof(IsAnimating), typeof(bool), typeof(ScenePreview), new PropertyMetadata(false, (d, _) => ((ScenePreview)d).UpdateAnimating()));

    private WallpaperScene? _scene;
    private bool _loaded;

    public ScenePreview()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        IsTabStop = false;
        Loaded += (_, _) =>
        {
            _loaded = true;
            Recreate();
        };
        Unloaded += (_, _) =>
        {
            _loaded = false;
            Release();
        };
    }

    public string SceneId
    {
        get => (string)GetValue(SceneIdProperty);
        set => SetValue(SceneIdProperty, value);
    }

    public bool IsAnimating
    {
        get => (bool)GetValue(IsAnimatingProperty);
        set => SetValue(IsAnimatingProperty, value);
    }

    private void Recreate()
    {
        Release();
        if (!_loaded || GalleryCatalog.Find(SceneId) is not { IsInteractive: true } item)
        {
            return;
        }

        _scene = SceneFactory.Create(item.Id);
        Content = _scene.Root;
        UpdateAnimating();
    }

    private void UpdateAnimating() => _scene?.SetAnimating(_loaded && IsAnimating);

    private void Release()
    {
        Content = null;
        _scene?.Dispose();
        _scene = null;
    }
}
