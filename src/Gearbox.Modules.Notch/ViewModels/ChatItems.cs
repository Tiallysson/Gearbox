using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Modules.Notch.Models;
using Microsoft.UI.Xaml;

namespace Gearbox.Modules.Notch.ViewModels;

public sealed partial class ChatMessageItem : ObservableObject
{
    public ChatMessageItem(ChatMessage message, bool pending)
    {
        IsUser = message.Role == ChatRole.User;
        Update(message, pending);
    }

    public bool IsUser { get; }

    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public Visibility UserVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AssistantVisibility => IsUser ? Visibility.Collapsed : Visibility.Visible;

    public string CopyLabel => ModuleStrings.Get("ChatCopyMessage");

    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Markdown { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility PendingVisibility { get; private set; }

    [ObservableProperty]
    public partial Visibility CopyVisibility { get; private set; }

    [ObservableProperty]
    public partial string RedactionNotice { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility RedactionVisibility { get; private set; }

    [ObservableProperty]
    public partial BoltMood Mood { get; private set; }

    [ObservableProperty]
    public partial bool IsMascotAnimated { get; private set; }

    public void Update(ChatMessage message, bool pending)
    {
        var content = message.Content;
        Text = content;
        RedactionNotice = message.RedactedSecrets switch
        {
            <= 0 => string.Empty,
            1 => ModuleStrings.Get("ChatRedactedOne"),
            var count => ModuleStrings.Format("ChatRedactedManyFormat", count),
        };
        RedactionVisibility = message.RedactedSecrets > 0 ? Visibility.Visible : Visibility.Collapsed;
        Markdown = IsUser ? string.Empty : content;
        PendingVisibility = pending && content.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        CopyVisibility = !IsUser && !pending && content.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Mood = !pending ? BoltMood.Idle : content.Length == 0 ? BoltMood.Thinking : BoltMood.Talking;
        IsMascotAnimated = pending;
    }
}

public sealed record ChatConversationChoice(string Id, string Title)
{
    public override string ToString() => Title;
}

public sealed record ChatModelChoice(string Id, string Name)
{
    public override string ToString() => Name;
}
