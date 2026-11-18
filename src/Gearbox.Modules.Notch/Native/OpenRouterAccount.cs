using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Notch.Native;

public enum OpenRouterConnection
{
    Disconnected,
    Connecting,
    Connected,
}

public sealed class OpenRouterAccount(ILogger<OpenRouterAccount> logger)
{
    private static readonly TimeSpan AuthorizationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RequestReadTimeout = TimeSpan.FromSeconds(10);
    private const int MaxRequestLine = 8192;

    private CancellationTokenSource? _connect;
    private OpenRouterConnection? _state;
    private bool _canceledByUser;

    public event EventHandler? Changed;

    public OpenRouterConnection State => _state ??= AiKeyVault.Has(AiProvider.OpenRouter)
        ? OpenRouterConnection.Connected
        : OpenRouterConnection.Disconnected;

    public string? LastErrorKey { get; private set; }

    internal string? Key => AiKeyVault.Get(AiProvider.OpenRouter);

    public async Task ConnectAsync(decimal creditLimit)
    {
        if (State == OpenRouterConnection.Connecting)
        {
            return;
        }

        using var connect = new CancellationTokenSource(AuthorizationTimeout);
        _connect = connect;
        _canceledByUser = false;
        SetState(OpenRouterConnection.Connecting, null);
        try
        {
            var key = await AuthorizeAsync(creditLimit, connect.Token);
            AiKeyVault.Set(AiProvider.OpenRouter, key);
            logger.LogInformation("OpenRouter conectado");
            SetState(OpenRouterConnection.Connected, null);
        }
        catch (OperationCanceledException)
        {
            SetState(OpenRouterConnection.Disconnected, _canceledByUser ? null : "OpenRouterConnectTimeout");
        }
        catch (Exception ex) when (ex is OpenRouterException or HttpRequestException or IOException or SocketException or JsonException or COMException)
        {
            logger.LogWarning(ex, "Não foi possível conectar ao OpenRouter");
            SetState(OpenRouterConnection.Disconnected, "OpenRouterConnectFailed");
        }
        finally
        {
            if (_connect == connect)
            {
                _connect = null;
            }
        }
    }

    public void CancelConnect()
    {
        _canceledByUser = true;
        _connect?.Cancel();
    }

    public void Disconnect()
    {
        CancelConnect();
        AiKeyVault.Remove(AiProvider.OpenRouter);
        SetState(OpenRouterConnection.Disconnected, null);
    }

    internal void ReportRejectedKey()
    {
        logger.LogWarning("O OpenRouter recusou a chave salva");
        AiKeyVault.Remove(AiProvider.OpenRouter);
        SetState(OpenRouterConnection.Disconnected, "OpenRouterKeyRejected");
    }

    private void SetState(OpenRouterConnection state, string? errorKey)
    {
        _state = state;
        LastErrorKey = errorKey;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<string> AuthorizeAsync(decimal creditLimit, CancellationToken cancellation)
    {
        var verifier = Pkce.CreateVerifier(RandomNumberGenerator.GetBytes(32));
        var state = Pkce.CreateVerifier(RandomNumberGenerator.GetBytes(16));
        var ipv4 = new TcpListener(IPAddress.Loopback, 0);
        ipv4.Start();
        var port = ((IPEndPoint)ipv4.LocalEndpoint).Port;
        var ipv6 = TryListenIpv6(port);
        try
        {
            var uri = OpenRouterOAuth.AuthorizeUri(OpenRouterOAuth.CallbackUrl(port), Pkce.Challenge(verifier), state, creditLimit);
            if (!await Windows.System.Launcher.LaunchUriAsync(uri))
            {
                throw new OpenRouterException("O navegador não abriu");
            }

            while (true)
            {
                using var client = await AcceptAsync(ipv4, ipv6, cancellation);
                var callback = await ReadCallbackAsync(client, cancellation);
                if (callback is null)
                {
                    await RespondAsync(client, "404 Not Found", string.Empty, cancellation);
                    continue;
                }

                var approved = callback.IsApprovedFor(state);
                await RespondAsync(
                    client,
                    "200 OK",
                    Page(approved ? "OpenRouterCallbackSuccess" : "OpenRouterCallbackFailed"),
                    cancellation);
                if (!approved)
                {
                    throw new OpenRouterException(callback.Error ?? "Autorização inválida");
                }

                using var http = new OpenRouterClient();
                return await http.ExchangeCodeAsync(callback.Code!, verifier, cancellation);
            }
        }
        finally
        {
            ipv4.Stop();
            ipv6?.Stop();
        }
    }

    private static TcpListener? TryListenIpv6(int port)
    {
        if (!Socket.OSSupportsIPv6)
        {
            return null;
        }

        var listener = new TcpListener(IPAddress.IPv6Loopback, port);
        try
        {
            listener.Start();
            return listener;
        }
        catch (SocketException)
        {
            listener.Stop();
            return null;
        }
    }

    private static async Task<TcpClient> AcceptAsync(TcpListener ipv4, TcpListener? ipv6, CancellationToken cancellation)
    {
        if (ipv6 is null)
        {
            return await ipv4.AcceptTcpClientAsync(cancellation).ConfigureAwait(false);
        }

        using var race = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var fromIpv4 = ipv4.AcceptTcpClientAsync(race.Token).AsTask();
        var fromIpv6 = ipv6.AcceptTcpClientAsync(race.Token).AsTask();
        var first = await Task.WhenAny(fromIpv4, fromIpv6).ConfigureAwait(false);
        await race.CancelAsync().ConfigureAwait(false);
        var other = first == fromIpv4 ? fromIpv6 : fromIpv4;
        _ = other.ContinueWith(
            task =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    task.Result.Dispose();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
        return await first.ConfigureAwait(false);
    }

    private static async Task<OAuthCallback?> ReadCallbackAsync(TcpClient client, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(RequestReadTimeout);
        var buffer = new byte[MaxRequestLine];
        var length = 0;
        var stream = client.GetStream();
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            length += read;
            if (Array.IndexOf(buffer, (byte)'\n', 0, length) >= 0)
            {
                break;
            }
        }

        var text = Encoding.ASCII.GetString(buffer, 0, length);
        var end = text.IndexOfAny(['\r', '\n']);
        return OpenRouterOAuth.ParseRequestLine(end < 0 ? text : text[..end]);
    }

    private static async Task RespondAsync(TcpClient client, string status, string html, CancellationToken cancellation)
    {
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        var stream = client.GetStream();
        await stream.WriteAsync(header, cancellation).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellation).ConfigureAwait(false);
        await stream.FlushAsync(cancellation).ConfigureAwait(false);
    }

    private static string Page(string messageKey)
    {
        var title = WebUtility.HtmlEncode(ModuleStrings.Get("OpenRouterCallbackTitle"));
        var message = WebUtility.HtmlEncode(ModuleStrings.Get(messageKey));
        return $"<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>{title}</title>" +
            "<style>body{font-family:system-ui,sans-serif;background:#0a0a0a;color:#fafafa;display:grid;place-items:center;height:100vh;margin:0}</style>" +
            $"</head><body><main><h1>{title}</h1><p>{message}</p></main></body></html>";
    }
}
