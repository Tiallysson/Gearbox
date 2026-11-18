using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Gearbox.Core.Settings;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Gearbox.Modules.Notch.Native;

internal sealed class ChatSession : IDisposable
{
    private static readonly TimeSpan RenderInterval = TimeSpan.FromMilliseconds(50);

    private readonly ChatStore _store;
    private readonly OpenRouterAccount _account;
    private readonly ISettingsService<ChatSettings> _settings;
    private readonly ILogger _logger;
    private readonly Action<Exception> _fault;
    private readonly OpenRouterClient _client;
    private readonly DispatcherQueueTimer _renderTimer;
    private readonly StringBuilder _reply = new();
    private CancellationTokenSource? _stream;
    private CancellationTokenSource? _loading;
    private ChatSettings _options;
    private List<ChatModel> _catalog = [];
    private bool _disposed;

    public ChatSession(
        DispatcherQueue dispatcher,
        ChatStore store,
        OpenRouterAccount account,
        ISettingsService<ChatSettings> settings,
        ILogger logger,
        Action<Exception> fault)
    {
        _store = store;
        _account = account;
        _settings = settings;
        _logger = logger;
        _fault = fault;
        _client = new OpenRouterClient(logger);
        _options = settings.Load();
        _renderTimer = dispatcher.CreateTimer();
        _renderTimer.Interval = RenderInterval;
        _renderTimer.IsRepeating = false;
        _renderTimer.Tick += (_, _) => FlushReply();
        Current = NewConversation(_options.Model);
        Models = AllowedModels();
    }

    public event EventHandler? ConversationsChanged;

    public event EventHandler? MessagesChanged;

    public List<ChatConversation> Conversations { get; private set; } = [];

    public ChatConversation Current { get; private set; }

    public List<ChatModel> Models { get; private set; } = [];

    public bool IsStreaming => _stream is not null;

    public string? ErrorText { get; private set; }

    public async void Start()
    {
        var loading = new CancellationTokenSource();
        _loading = loading;
        try
        {
            var conversations = await Task.Run(_store.LoadAll, loading.Token);
            if (_disposed)
            {
                return;
            }

            Conversations = conversations;
            if (Current.Messages.Count == 0 && conversations.Count > 0)
            {
                Current = conversations[0];
            }

            RaiseAll();
            await LoadModelsAsync(loading.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (!_disposed)
        {
            _fault(ex);
        }
    }

    public void ApplySettings(ChatSettings settings)
    {
        _options = settings;
        Models = AllowedModels();
        ConversationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NewConversation()
    {
        if (IsStreaming || Current.Messages.Count == 0)
        {
            return;
        }

        Current = NewConversation(Current.Model);
        ErrorText = null;
        RaiseAll();
    }

    public void Select(string id)
    {
        if (IsStreaming || id == Current.Id || Conversations.Find(c => c.Id == id) is not { } conversation)
        {
            return;
        }

        Current = conversation;
        ErrorText = null;
        Models = AllowedModels();
        RaiseAll();
    }

    public void DeleteCurrent()
    {
        if (IsStreaming)
        {
            return;
        }

        var deleted = Current;
        Conversations.RemoveAll(c => c.Id == deleted.Id);
        try
        {
            _store.Delete(deleted.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível apagar a conversa {Id}", deleted.Id);
        }

        Current = Conversations.Count > 0 ? Conversations[0] : NewConversation(deleted.Model);
        ErrorText = null;
        RaiseAll();
    }

    public void SetModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model == Current.Model)
        {
            return;
        }

        Current.Model = model;
        var settings = _settings.Load();
        settings.Model = model;
        _settings.Save(settings);
        if (Current.Messages.Count > 0)
        {
            Persist(Current);
        }
    }

    public bool Send(string text)
    {
        if (_disposed || IsStreaming || _account.Key is not { } key)
        {
            return false;
        }

        var violation = ChatGuardrails.CheckRequest(Current.Model, _options.AllowPaidModels, text);
        if (violation != ChatViolation.None)
        {
            if (violation != ChatViolation.EmptyInput)
            {
                ErrorText = ModuleStrings.Get("ChatGuard" + violation);
                MessagesChanged?.Invoke(this, EventArgs.Empty);
            }

            return false;
        }

        _ = RunAsync(key, ChatGuardrails.RedactSecrets(text.Trim()));
        return true;
    }

    private async Task RunAsync(string key, SecretRedaction prompt)
    {
        var conversation = Current;
        var isNew = conversation.Messages.Count == 0;
        if (isNew)
        {
            conversation.Title = ChatConversation.TitleFrom(prompt.Text);
            Conversations.Insert(0, conversation);
        }

        conversation.Messages.Add(new ChatMessage(ChatRole.User, prompt.Text, prompt.Count));
        var context = ChatContext.Window(conversation.Messages);
        conversation.Messages.Add(new ChatMessage(ChatRole.Assistant, string.Empty));
        conversation.UpdatedAt = DateTimeOffset.Now;
        _reply.Clear();
        ErrorText = null;
        var stream = new CancellationTokenSource();
        _stream = stream;
        RaiseAll();

        try
        {
            var request = new ChatRequest(
                key,
                conversation.Model,
                ModuleStrings.Get("ChatSystemPrompt"),
                context,
                _options.AllowPaidModels,
                _options.AllowDataCollection);
            await foreach (var chunk in _client.StreamChatAsync(request, stream.Token))
            {
                if (chunk.Delta is { } delta)
                {
                    _reply.Append(delta);
                    ScheduleRender();
                }

                if (chunk.Error is { } error)
                {
                    ErrorText = Describe(error);
                }
            }
        }
        catch (OpenRouterTimeoutException ex)
        {
            _logger.LogWarning("O OpenRouter ficou {Seconds} s sem responder", ChatGuardrails.IdleTimeout.TotalSeconds);
            ErrorText = ex.Message;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ChatGuardrailException ex)
        {
            ErrorText = ex.Message;
            Withdraw(conversation, isNew);
        }
        catch (OpenRouterException ex) when (ex.IsUnauthorized)
        {
            ErrorText = ModuleStrings.Get("ChatKeyRejected");
            _account.ReportRejectedKey();
        }
        catch (Exception ex) when (ex is OpenRouterException or HttpRequestException or IOException or SocketException or JsonException)
        {
            if (!_disposed)
            {
                _logger.LogWarning(ex, "Falha na conversa com o OpenRouter");
                ErrorText = Describe(ex.Message);
            }
        }
        catch (Exception ex) when (!_disposed)
        {
            _fault(ex);
        }
        finally
        {
            stream.Dispose();
            if (_stream == stream)
            {
                _stream = null;
            }
        }

        if (_disposed)
        {
            return;
        }

        _renderTimer.Stop();
        CompleteReply(conversation);
        if (conversation.Messages.Count > 0)
        {
            Persist(conversation);
        }

        RaiseAll();
    }

    private static string Describe(string error)
    {
        if (ChatGuardrails.IsDataPolicyError(error))
        {
            return ModuleStrings.Get("ChatDataPolicyBlocked");
        }

        return ModuleStrings.Format("ChatModelErrorFormat", error.Length == 0 ? ModuleStrings.Get("ChatUnknownError") : error);
    }

    private void Withdraw(ChatConversation conversation, bool isNew)
    {
        if (conversation.Messages.Count > 0 && conversation.Messages[^1].Role == ChatRole.Assistant)
        {
            conversation.Messages.RemoveAt(conversation.Messages.Count - 1);
        }

        if (conversation.Messages.Count > 0 && conversation.Messages[^1].Role == ChatRole.User)
        {
            conversation.Messages.RemoveAt(conversation.Messages.Count - 1);
        }

        if (isNew)
        {
            conversation.Title = string.Empty;
            Conversations.Remove(conversation);
        }

        _reply.Clear();
    }

    private List<ChatModel> AllowedModels()
    {
        var allowed = ChatGuardrails.Allowed(_catalog, _options.AllowPaidModels);
        return _options.AllowPaidModels ? ChatModels.WithSelected(allowed, Current.Model) : allowed;
    }

    public void Cancel() => _client.CancelCurrentRequest();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _renderTimer.Stop();
        _loading?.Cancel();
        _stream?.Cancel();
        _client.Dispose();
    }

    private async Task LoadModelsAsync(CancellationToken cancellation)
    {
        try
        {
            _catalog = await _client.ListModelsAsync(cancellation);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or TaskCanceledException && !cancellation.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Não foi possível listar os modelos do OpenRouter");
            _catalog = [];
        }

        if (_disposed)
        {
            return;
        }

        Models = AllowedModels();
        ConversationsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleRender()
    {
        if (!_renderTimer.IsRunning)
        {
            _renderTimer.Start();
        }
    }

    private void FlushReply()
    {
        if (_disposed || !IsStreaming || Current.Messages.Count == 0 || Current.Messages[^1].Role != ChatRole.Assistant)
        {
            return;
        }

        Current.Messages[^1] = new ChatMessage(ChatRole.Assistant, _reply.ToString());
        MessagesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CompleteReply(ChatConversation conversation)
    {
        if (conversation.Messages.Count == 0 || conversation.Messages[^1].Role != ChatRole.Assistant)
        {
            return;
        }

        var reply = _reply.ToString();
        if (reply.Length == 0)
        {
            conversation.Messages.RemoveAt(conversation.Messages.Count - 1);
        }
        else
        {
            conversation.Messages[^1] = new ChatMessage(ChatRole.Assistant, reply);
        }

        conversation.UpdatedAt = DateTimeOffset.Now;
        Conversations.Remove(conversation);
        Conversations.Insert(0, conversation);
    }

    private void Persist(ChatConversation conversation)
    {
        try
        {
            _store.Save(conversation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível salvar a conversa {Id}", conversation.Id);
        }
    }

    private void RaiseAll()
    {
        ConversationsChanged?.Invoke(this, EventArgs.Empty);
        MessagesChanged?.Invoke(this, EventArgs.Empty);
    }

    private ChatConversation NewConversation(string model) =>
        new() { Model = ChatGuardrails.DefaultModel(model, _options.AllowPaidModels), UpdatedAt = DateTimeOffset.Now };
}
