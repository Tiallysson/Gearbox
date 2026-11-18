using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gearbox.Modules.Notch.Models;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Notch.Services.AI;

internal sealed class OpenRouterException(string message, bool unauthorized = false) : Exception(message)
{
    public bool IsUnauthorized { get; } = unauthorized;
}

internal sealed class ChatGuardrailException(ChatViolation violation) : Exception(ModuleStrings.Get("ChatGuard" + violation))
{
    public ChatViolation Violation { get; } = violation;
}

internal sealed class OpenRouterTimeoutException(Exception inner) : Exception(ModuleStrings.Get("ChatTimeout"), inner);

internal sealed record ChatRequest(
    string Key,
    string Model,
    string SystemPrompt,
    IReadOnlyList<ChatMessage> Messages,
    bool AllowPaidModels,
    bool AllowDataCollection);

internal sealed class OpenRouterClient : IDisposable
{
    private const string BaseUrl = "https://openrouter.ai/api/v1/";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger? _logger;
    private CancellationTokenSource? _current;
    private volatile bool _canceledByUser;

    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestHeaders =
        {
            UserAgent = { new ProductInfoHeaderValue("Gearbox", "1.0") },
        },
    };

    public OpenRouterClient(ILogger? logger = null)
    {
        _logger = logger;
        _http.DefaultRequestHeaders.Add("X-Title", "Gearbox");
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }

    public async Task<KeyUsage?> GetKeyUsageAsync(string key, CancellationToken cancellation)
    {
        using var timeout = Deadline(cancellation, TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, "key");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new OpenRouterException(response.ReasonPhrase ?? string.Empty, unauthorized: true);
        }

        response.EnsureSuccessStatusCode();
        return ChatGuardrails.ParseKeyUsage(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
    }

    public async Task<string> ExchangeCodeAsync(string code, string verifier, CancellationToken cancellation)
    {
        using var timeout = Deadline(cancellation, TimeSpan.FromSeconds(30));
        var body = new JsonObject
        {
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["code_challenge_method"] = Pkce.Method,
        };
        using var response = await _http.PostAsJsonAsync("auth/keys", body, timeout.Token).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new OpenRouterException(ChatStream.ErrorMessage(json) ?? response.ReasonPhrase ?? string.Empty);
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("key", out var key) && key.GetString() is { Length: > 0 } value
            ? value
            : throw new OpenRouterException("Resposta sem chave");
    }

    public async Task<List<ChatModel>> ListModelsAsync(CancellationToken cancellation)
    {
        using var timeout = Deadline(cancellation, TimeSpan.FromSeconds(20));
        using var response = await _http.GetAsync("models", timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return ChatModels.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamChatAsync(ChatRequest chat, [EnumeratorCancellation] CancellationToken cancellation)
    {
        if (!await _gate.WaitAsync(0, cancellation).ConfigureAwait(false))
        {
            throw new ChatGuardrailException(ChatViolation.Busy);
        }

        _canceledByUser = false;
        var current = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        Interlocked.Exchange(ref _current, current);
        try
        {
            var input = chat.Messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? string.Empty;
            Enforce(ChatGuardrails.CheckRequest(chat.Model, chat.AllowPaidModels, input));
            current.CancelAfter(ChatGuardrails.IdleTimeout);
            Enforce(ChatGuardrails.CheckUsage(await TryGetUsagePercentAsync(chat.Key, current.Token).ConfigureAwait(false)));

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(Payload(chat)),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chat.Key);

            using var response = await OpenAsync(request, current, cancellation).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(current.Token).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            while (await NextLineAsync(reader, current, cancellation).ConfigureAwait(false) is { } line)
            {
                current.CancelAfter(ChatGuardrails.IdleTimeout);
                if (ChatStream.ParseLine(line) is not { } chunk)
                {
                    continue;
                }

                yield return chunk;
                if (chunk.IsDone)
                {
                    yield break;
                }
            }
        }
        finally
        {
            Interlocked.CompareExchange(ref _current, null, current);
            current.Dispose();
            _gate.Release();
        }
    }

    public void CancelCurrentRequest()
    {
        _canceledByUser = true;
        try
        {
            Volatile.Read(ref _current)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<HttpResponseMessage> OpenAsync(HttpRequestMessage request, CancellationTokenSource current, CancellationToken caller)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, current.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (IsIdleTimeout(current, caller))
        {
            throw new OpenRouterTimeoutException(ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(current.Token).ConfigureAwait(false);
            throw new OpenRouterException(
                ChatStream.ErrorMessage(body) ?? response.ReasonPhrase ?? string.Empty,
                response.StatusCode == HttpStatusCode.Unauthorized);
        }
    }

    private async Task<string?> NextLineAsync(StreamReader reader, CancellationTokenSource current, CancellationToken caller)
    {
        try
        {
            return await reader.ReadLineAsync(current.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (IsIdleTimeout(current, caller))
        {
            throw new OpenRouterTimeoutException(ex);
        }
    }

    private bool IsIdleTimeout(CancellationTokenSource current, CancellationToken caller) =>
        current.IsCancellationRequested && !caller.IsCancellationRequested && !_canceledByUser;

    private static JsonObject Payload(ChatRequest chat)
    {
        var messages = ChatGuardrails.TrimConversationHistory(
            [new ChatMessage(ChatRole.System, chat.SystemPrompt), .. chat.Messages]);
        var history = new JsonArray();
        foreach (var message in messages)
        {
            history.Add(new JsonObject
            {
                ["role"] = message.Role switch
                {
                    ChatRole.System => "system",
                    ChatRole.User => "user",
                    _ => "assistant",
                },
                ["content"] = message.Role == ChatRole.User ? ChatGuardrails.SanitizeUserPrompt(message.Content) : message.Content,
            });
        }

        return new JsonObject
        {
            ["model"] = chat.Model,
            ["messages"] = history,
            ["stream"] = true,
            ["max_tokens"] = ChatGuardrails.MaxTokens,
            ["provider"] = new JsonObject { ["data_collection"] = chat.AllowDataCollection ? "allow" : "deny" },
        };
    }

    private static void Enforce(ChatViolation violation)
    {
        if (violation != ChatViolation.None)
        {
            throw new ChatGuardrailException(violation);
        }
    }

    private async Task<double?> TryGetUsagePercentAsync(string key, CancellationToken cancellation)
    {
        try
        {
            return (await GetKeyUsageAsync(key, cancellation).ConfigureAwait(false))?.Percent;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException && !cancellation.IsCancellationRequested)
        {
            _logger?.LogWarning(ex, "Não foi possível consultar o uso da chave do OpenRouter; enviando mesmo assim");
            return null;
        }
    }

    private static CancellationTokenSource Deadline(CancellationToken cancellation, TimeSpan after)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        source.CancelAfter(after);
        return source;
    }
}
