using Gearbox.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Gearbox.Modules.Wallpaper.Native;

/// <summary>
/// Vídeo em loop, sem som, numa janela WinUI sem moldura presa atrás dos ícones da área de trabalho.
/// </summary>
/// <remarks>
/// A janela precisa ser exibida <em>antes</em> de virar filha do WorkerW: reparentar uma janela WinUI que
/// ainda não apareceu derruba o processo (E_POINTER no Microsoft.UI.Xaml). Por isso ela nasce fora da tela,
/// aparece sem ativar e só então é presa e posicionada.
/// </remarks>
internal sealed class VideoWallpaperWindow : IDisposable
{
    private static readonly TimeSpan AttachDelay = TimeSpan.FromMilliseconds(300);

    private readonly ILogger _logger;
    private readonly DispatcherQueue _dispatcher;
    private readonly Window _window;
    private readonly MediaPlayerElement _element;
    private readonly MediaPlayer _player;
    private readonly nint _hwnd;

    // Campo: um timer referenciado só por variável local pode ser coletado antes de disparar.
    private readonly DispatcherQueueTimer _attachTimer;

    private PixelRect _bounds;
    private bool _paused;
    private bool _disposed;

    public VideoWallpaperWindow(string path, WallpaperFit fit, PixelRect bounds, ILogger logger)
    {
        _logger = logger;
        _dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("O wallpaper em vídeo precisa ser criado na thread de UI.");
        Path = path;
        _bounds = bounds;

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

        _window = new Window
        {
            Content = new Grid
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
                Children = { _element },
            },
        };
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

        var appWindow = _window.AppWindow;
        appWindow.IsShownInSwitchers = false;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // Fora da tela até ser presa: assim não pisca por cima das outras janelas.
        appWindow.MoveAndResize(new RectInt32(-32000, -32000, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height)));
        appWindow.Show(activateWindow: false);

        _attachTimer = _dispatcher.CreateTimer();
        _attachTimer.Interval = AttachDelay;
        _attachTimer.IsRepeating = false;
        _attachTimer.Tick += (_, _) => Attach();
        _attachTimer.Start();

        _player.Source = MediaSource.CreateFromUri(new Uri(path));
    }

    /// <summary>O vídeo não pôde ser tocado (arquivo corrompido, codec ausente...). Disparado na thread de UI.</summary>
    public event EventHandler<string>? Failed;

    public string Path { get; }

    public void SetFit(WallpaperFit fit) => _element.Stretch = ToStretch(fit);

    /// <summary>Pausa ou retoma. Devolve <c>true</c> se o estado mudou.</summary>
    public bool SetPaused(bool paused)
    {
        if (_disposed || paused == _paused)
        {
            return false;
        }

        _paused = paused;
        if (paused)
        {
            _player.Pause(); // O último quadro continua na tela.
        }
        else
        {
            _player.Play();
        }

        return true;
    }

    /// <summary>Move para outro monitor/resolução.</summary>
    public void SetBounds(PixelRect bounds)
    {
        _bounds = bounds;
        Attach();
    }

    /// <summary>Prende de novo na camada da área de trabalho (ex.: depois que o Explorer reiniciou).</summary>
    public void Attach()
    {
        if (_disposed)
        {
            return;
        }

        var host = DesktopLayer.FindHost();
        if (host == 0)
        {
            _logger.LogWarning("Camada da área de trabalho não encontrada; o vídeo fica oculto até o Explorer voltar");
            return;
        }

        if (!DesktopLayer.Attach(_hwnd, host, _bounds))
        {
            _logger.LogWarning("Não foi possível prender o vídeo na área de trabalho (erro {Error})", NativeWindow.LastError);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _attachTimer.Stop();
        _player.MediaFailed -= OnMediaFailed;
        _element.SetMediaPlayer(null);
        _player.Dispose();
        _window.Close();
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        // Chega numa thread do player.
        var message = string.IsNullOrWhiteSpace(args.ErrorMessage) ? args.Error.ToString() : args.ErrorMessage;
        _dispatcher.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                Failed?.Invoke(this, message);
            }
        });
    }

    private static Stretch ToStretch(WallpaperFit fit) => fit switch
    {
        WallpaperFit.Fit => Stretch.Uniform,
        WallpaperFit.Stretch => Stretch.Fill,
        WallpaperFit.Center => Stretch.None,
        _ => Stretch.UniformToFill, // Fill, Tile e Span
    };
}
