using System.Collections.ObjectModel;
using Gearbox.Modules.Notch.Models;
using Gearbox.Modules.Notch.Services.AI;
using Gearbox.Modules.Notch.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace Gearbox.Modules.Notch.Views.Notch;

public sealed partial class NotchView
{
    private const string SendGlyph = "";
    private const string StopGlyph = "";
    private const double FollowThreshold = 24;
    private const int CounterWarningLength = ChatGuardrails.MaxInputLength - 2_000;

    private readonly ObservableCollection<ChatMessageItem> _chatItems = [];
    private bool _updatingChatChoices;
    private bool _chatStreaming;
    private bool _chatScrolling;
    private UIElement? _modePanel;
    private BoltMood _boltPeekMood;
    private string _boltPeekText = string.Empty;

    public event EventHandler<string>? ChatSendRequested;

    public event EventHandler? ChatCancelRequested;

    public event EventHandler? ChatNewRequested;

    public event EventHandler? ChatDeleteRequested;

    public event EventHandler<string>? ChatConversationSelected;

    public event EventHandler<string>? ChatModelSelected;

    public event EventHandler? ChatConnectRequested;

    public event EventHandler? ChatCollapseRequested;

    private void InitializeChat()
    {
        ChatInput.MaxLength = ChatGuardrails.MaxInputLength;
        ChatMessageList.ItemsSource = _chatItems;
        ChatPage.HorizontalAlignment = HorizontalAlignment.Left;
        ChatPage.Width = NotchLayout.ExpandedChatShape.Size.Width - ExpandedPanel.Padding.Left - ExpandedPanel.Padding.Right;
    }

    public bool IsChatInputFocused => ChatInput.FocusState != FocusState.Unfocused;

    public void FocusChatInput()
    {
        if (ChatInputRow.Visibility == Visibility.Visible)
        {
            ChatInput.Focus(FocusState.Programmatic);
        }
    }

    public void SetChatConnection(OpenRouterConnection connection, string? errorKey)
    {
        var connected = connection == OpenRouterConnection.Connected;
        var connecting = connection == OpenRouterConnection.Connecting;
        var chat = connected ? Visibility.Visible : Visibility.Collapsed;
        ChatHeader.Visibility = ChatScroll.Visibility = ChatInputRow.Visibility = chat;
        ChatConnectPanel.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        if (!connected)
        {
            ChatEmptyPanel.Visibility = ChatErrorText.Visibility = Visibility.Collapsed;
        }

        ChatConnectText.Text = ModuleStrings.Get(errorKey ?? (connecting ? "ChatConnecting" : "ChatConnectPrompt"));
        ChatConnectMascot.Mood = connecting ? BoltMood.Thinking : BoltMood.Wave;
        UpdateMascots();
        var label = ModuleStrings.Get(connecting ? "ChatConnectCancel" : "ChatConnectButton");
        ChatConnectButton.Content = label;
        AutomationProperties.SetName(ChatConnectButton, label);
    }

    public void SetChatConversations(IReadOnlyList<ChatConversation> conversations, ChatConversation current)
    {
        var choices = conversations.Select(c => new ChatConversationChoice(c.Id, TitleOf(c))).ToList();
        if (!choices.Exists(c => c.Id == current.Id))
        {
            choices.Insert(0, new ChatConversationChoice(current.Id, TitleOf(current)));
        }

        _updatingChatChoices = true;
        try
        {
            ChatConversationBox.ItemsSource = choices;
            ChatConversationBox.SelectedItem = choices.Find(c => c.Id == current.Id);
        }
        finally
        {
            _updatingChatChoices = false;
        }

        ChatDeleteButton.IsEnabled = !_chatStreaming && current.Messages.Count > 0;
        ChatNewButton.IsEnabled = !_chatStreaming && current.Messages.Count > 0;

        static string TitleOf(ChatConversation conversation) =>
            string.IsNullOrWhiteSpace(conversation.Title) ? ModuleStrings.Get("ChatNewConversation") : conversation.Title;
    }

    public void SetChatModels(IReadOnlyList<ChatModel> models, string selected)
    {
        var choices = models.Select(m => new ChatModelChoice(m.Id, m.Name)).ToList();
        _updatingChatChoices = true;
        try
        {
            ChatModelBox.ItemsSource = choices;
            ChatModelBox.SelectedItem = choices.Find(c => c.Id == selected);
            ChatModelBox.PlaceholderText = ModuleStrings.Get("ChatModelBlocked");
        }
        finally
        {
            _updatingChatChoices = false;
        }
    }

    public void SetChatMessages(IReadOnlyList<ChatMessage> messages, bool streaming, string? error)
    {
        var follow = !_chatScrolling && ChatScroll.VerticalOffset >= ChatScroll.ScrollableHeight - FollowThreshold;
        var countBefore = _chatItems.Count;
        var replaced = false;
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            var pending = streaming && index == messages.Count - 1 && message.Role == ChatRole.Assistant;
            if (index < _chatItems.Count && _chatItems[index].IsUser == (message.Role == ChatRole.User))
            {
                _chatItems[index].Update(message, pending);
            }
            else if (index < _chatItems.Count)
            {
                _chatItems[index] = new ChatMessageItem(message, pending);
                replaced = true;
            }
            else
            {
                _chatItems.Add(new ChatMessageItem(message, pending));
            }
        }

        while (_chatItems.Count > messages.Count)
        {
            _chatItems.RemoveAt(_chatItems.Count - 1);
        }

        var connected = ChatInputRow.Visibility == Visibility.Visible;
        ChatEmptyPanel.Visibility = connected && messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateMascots();
        ChatErrorText.Text = error ?? string.Empty;
        ChatErrorText.Visibility = connected && error is not null ? Visibility.Visible : Visibility.Collapsed;
        SetChatStreaming(streaming);

        if (!follow || _chatItems.Count == 0)
        {
            return;
        }

        if (replaced || _chatItems.Count != countBefore)
        {
            ScrollChatToEnd();
        }
        else if (ChatScroll.VerticalOffset < ChatScroll.ScrollableHeight)
        {
            ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null, true);
        }
    }

    private void OnChatScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e) => _chatScrolling = e.IsIntermediate;

    private void ScrollChatToEnd()
    {
        ChatScroll.UpdateLayout();
        if (ChatMessageList.GetOrCreateElement(_chatItems.Count - 1) is { } last)
        {
            last.UpdateLayout();
            last.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 1, AnimationDesired = false });
        }
    }

    private void SetChatStreaming(bool streaming)
    {
        _chatStreaming = streaming;
        ChatSendIcon.Glyph = streaming ? StopGlyph : SendGlyph;
        var label = ModuleStrings.Get(streaming ? "ChatStopToolTip" : "ChatSendToolTip");
        ToolTipService.SetToolTip(ChatSendButton, label);
        AutomationProperties.SetName(ChatSendButton, label);
        ChatConversationBox.IsEnabled = !streaming;
        if (streaming)
        {
            ChatNewButton.IsEnabled = ChatDeleteButton.IsEnabled = false;
        }
    }

    private void SendChatInput()
    {
        if (_chatStreaming || string.IsNullOrWhiteSpace(ChatInput.Text))
        {
            return;
        }

        ChatSendRequested?.Invoke(this, ChatInput.Text);
    }

    public void ClearChatInput() => ChatInput.Text = string.Empty;

    public void SetBoltPeek(BoltMood mood, string text)
    {
        _boltPeekMood = mood;
        _boltPeekText = text;
        if (_peek == PeekKind.Bolt)
        {
            PeekMascot.Mood = mood;
            PeekText.Text = text;
        }
    }

    private void UpdateMascots()
    {
        var chatOnScreen = _modePanel == ExpandedPanel && _page == NotchPage.Chat;
        ChatEmptyMascot.IsAnimated = chatOnScreen && ChatEmptyPanel.Visibility == Visibility.Visible;
        ChatConnectMascot.IsAnimated = chatOnScreen && ChatConnectPanel.Visibility == Visibility.Visible;
        PeekMascot.IsAnimated = _modePanel == PeekPanel && _peek == PeekKind.Bolt;
    }

    private void OnChatInputTextChanged(object sender, TextChangedEventArgs e)
    {
        var length = ChatInput.Text.Length;
        ChatCounterText.Visibility = length >= CounterWarningLength ? Visibility.Visible : Visibility.Collapsed;
        if (length >= CounterWarningLength)
        {
            ChatCounterText.Text = ModuleStrings.Format("ChatCounterFormat", length, ChatGuardrails.MaxInputLength);
        }
    }

    private void OnChatInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        if (!shift)
        {
            e.Handled = true;
            SendChatInput();
        }
    }

    private void OnChatKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            ChatCollapseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnChatSendClick(object sender, RoutedEventArgs e)
    {
        if (_chatStreaming)
        {
            ChatCancelRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            SendChatInput();
        }
    }

    private void OnChatNewClick(object sender, RoutedEventArgs e) => ChatNewRequested?.Invoke(this, EventArgs.Empty);

    private void OnChatDeleteClick(object sender, RoutedEventArgs e) => ChatDeleteRequested?.Invoke(this, EventArgs.Empty);

    private void OnChatConnectClick(object sender, RoutedEventArgs e) => ChatConnectRequested?.Invoke(this, EventArgs.Empty);

    private void OnChatConversationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingChatChoices && ChatConversationBox.SelectedItem is ChatConversationChoice choice)
        {
            ChatConversationSelected?.Invoke(this, choice.Id);
        }
    }

    private void OnChatModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingChatChoices && ChatModelBox.SelectedItem is ChatModelChoice choice)
        {
            ChatModelSelected?.Invoke(this, choice.Id);
        }
    }

    private void OnChatCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string { Length: > 0 } text })
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
    }
}
