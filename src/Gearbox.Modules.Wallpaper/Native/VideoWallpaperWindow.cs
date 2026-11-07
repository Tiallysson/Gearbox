using Gearbox.Interop.Geometry;
using Gearbox.Modules.Wallpaper.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Gearbox.Modules.Wallpaper.Native;

internal sealed class VideoWallpaperWindow : IAnimatedWallpaper
{
    private readonly DispatcherQueue _dispatcher;
    private readonly DesktopHostWindow _host;
    private readonly MediaPlayerElement _element;
    private readonly MediaPlayer _player;
    private bool _paused;
    private bool _disposed;

    public VideoWallpaperWindow(string path, WallpaperFitMode fit, PixelRect bounds, ILogger logger)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("O wallpaper em vídeo precisa ser criado na thread de UI.");
        Source = path;

        _player = new MediaPlayer
        {
            IsLoopingEnabled = true,
            IsMuted = true,
            AutoPlay = true,
            AudioCategory = MediaPlayerAudioCategory.Other,
        };
        _player.MediaFailed += OnMediaFailed;

        _element = new MediaPlayerElement
        {
            AreTransportControlsEnabled = false,
            Stretch = ToStretch(fit),
            IsHitTestVisible = false,
        };
        _element.SetMediaPlayer(_player);

        try
        {
            _host = new DesktopHostWindow(
                new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Black), Children = { _element } },
                bounds,
                logger);
            _player.Source = MediaSource.CreateFromUri(new Uri(path));
        }
        catch
        {
            _player.MediaFailed -= OnMediaFailed;
            _element.SetMediaPlayer(null);
            _player.Dispose();
            throw;
        }
    }

    public event EventHandler<string>? Failed;

    public string Source { get; }

    public void SetFit(WallpaperFitMode fit) => _element.Stretch = ToStretch(fit);

    public bool SetPaused(bool paused)
    {
        if (_disposed || paused == _paused)
        {
            return false;
        }

        _paused = paused;
        if (paused)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }

        return true;
    }

    public void SetBounds(PixelRect bounds) => _host.SetBounds(bounds);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player.MediaFailed -= OnMediaFailed;
        _element.SetMediaPlayer(null);
        _player.Dispose();
        _host.Dispose();
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        var message = string.IsNullOrWhiteSpace(args.ErrorMessage) ? args.Error.ToString() : args.ErrorMessage;
        _dispatcher.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                Failed?.Invoke(this, message);
            }
        });
    }

    private static Stretch ToStretch(WallpaperFitMode fit) => fit switch
    {
        WallpaperFitMode.Fit => Stretch.Uniform,
        WallpaperFitMode.Stretch => Stretch.Fill,
        WallpaperFitMode.Center => Stretch.None,
        _ => Stretch.UniformToFill,
    };
}
