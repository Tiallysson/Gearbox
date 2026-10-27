using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Gearbox.Modules.Notch.Native;

/// <summary>O que está tocando agora.</summary>
/// <param name="ArtworkVersion">Muda quando a capa pode ter mudado (evita recarregar a imagem a cada play/pause).</param>
public sealed record MediaSnapshot(
    string Title,
    string Artist,
    bool IsPlaying,
    bool CanGoPrevious,
    bool CanGoNext,
    IRandomAccessStreamReference? Artwork,
    int ArtworkVersion)
{
    public string TrackKey => Title + "\n" + Artist;
}

public enum MediaCommand
{
    TogglePlayPause,
    Previous,
    Next,
}

/// <summary>
/// Acompanha a sessão de mídia atual do Windows (<c>GlobalSystemMediaTransportControlsSessionManager</c>):
/// Spotify, navegadores, players... qualquer app que apareça no controle de mídia da barra de tarefas.
/// </summary>
/// <remarks>
/// Os eventos do WinRT chegam em threads quaisquer e são levados para a thread de UI; <see cref="Changed"/> e
/// <see cref="Current"/> só são usados nela. Falhas do app tocando (fechou no meio de uma leitura) só são
/// logadas: não são falhas do módulo.
/// </remarks>
internal sealed class MediaSessionWatcher(DispatcherQueue dispatcher, ILogger logger) : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private GlobalSystemMediaTransportControlsSessionMediaProperties? _properties;
    private bool _propertiesStale = true;
    private int _refreshVersion;
    private int _artworkVersion;
    private bool _disposed;

    /// <summary><see cref="Current"/> mudou. Disparado na thread de UI.</summary>
    public event EventHandler? Changed;

    public MediaSnapshot? Current { get; private set; }

    /// <summary>Conecta ao gerenciador de sessões. Não lança: em caso de falha, loga e fica sem mídia.</summary>
    public async Task StartAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed)
            {
                return;
            }

            _manager = manager;
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            AttachCurrentSession();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Controles de mídia do Windows indisponíveis; a ilha fica sem mídia");
        }
    }

    public async Task SendAsync(MediaCommand command)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        try
        {
            _ = command switch
            {
                MediaCommand.Previous => await session.TrySkipPreviousAsync(),
                MediaCommand.Next => await session.TrySkipNextAsync(),
                _ => await session.TryTogglePlayPauseAsync(),
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Comando de mídia {Command} falhou", command);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachSession();
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager = null;
        }

        Current = null;
    }

    private void AttachCurrentSession()
    {
        DetachSession();
        _session = _manager?.GetCurrentSession();
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        }

        _ = RefreshAsync(reloadProperties: true);
    }

    private void DetachSession()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session = null;
        }

        _properties = null;
        _propertiesStale = true;
    }

    private async Task RefreshAsync(bool reloadProperties)
    {
        // Leituras assíncronas podem terminar fora de ordem: só a mais recente publica o resultado.
        // Uma leitura descartada não pode perder a troca de faixa: a marca só sai quando as propriedades chegam.
        var version = ++_refreshVersion;
        var session = _session;
        _propertiesStale |= reloadProperties;
        try
        {
            if (session is null)
            {
                Publish(null);
                return;
            }

            if (_propertiesStale || _properties is null)
            {
                var properties = await session.TryGetMediaPropertiesAsync();
                if (_disposed || version != _refreshVersion)
                {
                    return;
                }

                _properties = properties;
                _propertiesStale = false;
                _artworkVersion++;
            }

            var playback = session.GetPlaybackInfo();
            var controls = playback?.Controls;
            var current = _properties;
            Publish(current is null || string.IsNullOrWhiteSpace(current.Title)
                ? null
                : new MediaSnapshot(
                    current.Title,
                    string.IsNullOrWhiteSpace(current.Artist) ? current.AlbumArtist : current.Artist,
                    playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    controls?.IsPreviousEnabled ?? false,
                    controls?.IsNextEnabled ?? false,
                    current.Thumbnail,
                    _artworkVersion));
        }
        catch (Exception ex)
        {
            // O app que estava tocando fechou no meio da leitura, por exemplo.
            logger.LogDebug(ex, "Não foi possível ler a sessão de mídia");
            if (!_disposed && version == _refreshVersion)
            {
                Publish(null);
            }
        }
    }

    private void Publish(MediaSnapshot? snapshot)
    {
        if (_disposed || snapshot == Current)
        {
            return;
        }

        Current = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) =>
        Enqueue(AttachCurrentSession);

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) =>
        Enqueue(() => _ = RefreshAsync(reloadProperties: true));

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) =>
        Enqueue(() => _ = RefreshAsync(reloadProperties: false));

    private void Enqueue(Action action) => dispatcher.TryEnqueue(() =>
    {
        if (!_disposed)
        {
            action();
        }
    });
}
