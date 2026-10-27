using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.Devices.Radios;
using Windows.Networking.Connectivity;

namespace Gearbox.Modules.Notch.Native;

/// <summary>
/// Rádios Wi‑Fi e Bluetooth (<c>Windows.Devices.Radios</c>, a mesma API das Configurações Rápidas) e a rede Wi‑Fi
/// conectada.
/// </summary>
/// <remarks>
/// Os eventos do WinRT chegam em threads quaisquer e são levados para a thread de UI; <see cref="Changed"/>,
/// <see cref="WiFi"/> e <see cref="Bluetooth"/> só são usados nela. Ligar/desligar é ação do usuário: o módulo não
/// desfaz isso ao parar. Falhas (rádio sumiu, modo avião, chave física) só são logadas.
/// </remarks>
internal sealed class ConnectivityWatcher(DispatcherQueue dispatcher, ILogger logger) : IDisposable
{
    private Radio? _wifi;
    private Radio? _bluetooth;
    private int _refreshVersion;
    private bool _allowed;
    private bool _disposed;

    /// <summary><see cref="WiFi"/> ou <see cref="Bluetooth"/> mudou. Disparado na thread de UI.</summary>
    public event EventHandler? Changed;

    public RadioInfo WiFi { get; private set; } = RadioInfo.Unavailable;

    public RadioInfo Bluetooth { get; private set; } = RadioInfo.Unavailable;

    /// <summary>Por onde o PC chega à internet agora (cabo, Wi‑Fi...).</summary>
    public NetworkLink Link { get; private set; } = NetworkLink.None;

    /// <summary>Pede acesso aos rádios e lê o estado. Não lança: em caso de falha, loga e fica sem os botões.</summary>
    public async Task StartAsync()
    {
        try
        {
            var access = await Radio.RequestAccessAsync();
            if (_disposed)
            {
                return;
            }

            if (access != RadioAccessStatus.Allowed)
            {
                logger.LogWarning("Acesso aos rádios negado ({Access}); a ilha fica sem Wi‑Fi/Bluetooth", access);
                return;
            }

            _allowed = true;
            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rádios indisponíveis; a ilha fica sem Wi‑Fi/Bluetooth");
        }
    }

    /// <summary>Relê a lista de rádios (um adaptador USB pode ter sido conectado). Não lança.</summary>
    public async Task RefreshAsync()
    {
        if (!_allowed || _disposed)
        {
            return;
        }

        // Leituras podem terminar fora de ordem: só a mais recente troca os rádios.
        var version = ++_refreshVersion;
        try
        {
            var radios = await Radio.GetRadiosAsync();
            if (_disposed || version != _refreshVersion)
            {
                return;
            }

            Attach(ref _wifi, radios.FirstOrDefault(r => r.Kind == RadioKind.WiFi));
            Attach(ref _bluetooth, radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth));
            Publish();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Não foi possível listar os rádios");
        }
    }

    /// <summary>Liga ou desliga o rádio. O novo estado chega por <see cref="Changed"/>. Não lança.</summary>
    public async Task ToggleAsync(QuickRadio kind)
    {
        var (radio, info) = kind == QuickRadio.WiFi ? (_wifi, WiFi) : (_bluetooth, Bluetooth);
        if (radio is null || QuickControls.ToggleTarget(info) is not { } target)
        {
            return;
        }

        try
        {
            var result = await radio.SetStateAsync(target == RadioPower.On ? RadioState.On : RadioState.Off);
            if (result != RadioAccessStatus.Allowed)
            {
                // Modo avião ou chave física, por exemplo.
                logger.LogInformation("O Windows recusou mudar o rádio {Kind}: {Result}", kind, result);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao mudar o rádio {Kind}", kind);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        Attach(ref _wifi, null);
        Attach(ref _bluetooth, null);
    }

    private void Attach(ref Radio? field, Radio? radio)
    {
        if (field is not null && radio is not null && field.Kind == radio.Kind && field.Name == radio.Name)
        {
            return; // Mesmo rádio: mantém a assinatura.
        }

        if (field is not null)
        {
            field.StateChanged -= OnRadioStateChanged;
        }

        field = radio;
        if (field is not null)
        {
            field.StateChanged += OnRadioStateChanged;
        }
    }

    private void Publish()
    {
        if (_disposed)
        {
            return;
        }

        var link = ReadLink();
        var wifi = Read(_wifi);
        if (wifi.Power == RadioPower.On)
        {
            wifi = wifi with { NetworkName = link.Kind == NetworkKind.WiFi ? link.Name : null };
        }

        var bluetooth = Read(_bluetooth);
        if (wifi == WiFi && bluetooth == Bluetooth && link == Link)
        {
            return;
        }

        WiFi = wifi;
        Bluetooth = bluetooth;
        Link = link;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static RadioInfo Read(Radio? radio) => radio?.State switch
    {
        RadioState.On => new RadioInfo(RadioPower.On),

        // Disabled = modo avião/chave física: aparece desligado; ligar é recusado e logado.
        RadioState.Off or RadioState.Disabled => new RadioInfo(RadioPower.Off),
        _ => RadioInfo.Unavailable,
    };

    /// <summary>Conexão com a internet: tipo pelo adaptador (IANA ifType), nome do perfil e velocidade do link.</summary>
    private NetworkLink ReadLink()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            var adapter = profile?.NetworkAdapter;
            var kind = NetworkLinks.Classify(profile is not null, profile?.IsWlanConnectionProfile == true, adapter?.IanaInterfaceType ?? 0);
            return kind == NetworkKind.None
                ? NetworkLink.None
                : new NetworkLink(kind, profile!.ProfileName, adapter?.InboundMaxBitsPerSecond ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Não foi possível ler a rede conectada");
            return NetworkLink.None;
        }
    }

    private void OnRadioStateChanged(Radio sender, object args) => Enqueue(Publish);

    private void OnNetworkStatusChanged(object sender) => Enqueue(Publish);

    private void Enqueue(Action action) => dispatcher.TryEnqueue(() =>
    {
        if (!_disposed)
        {
            action();
        }
    });
}
