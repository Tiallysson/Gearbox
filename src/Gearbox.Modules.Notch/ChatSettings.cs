using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch;

public sealed class ChatSettings
{
    public const string SettingsId = "NotchChat";

    public string Model { get; set; } = ChatConversation.DefaultModel;

    public decimal KeyCreditLimit { get; set; }

    public bool AllowPaidModels { get; set; }

    public bool AllowDataCollection { get; set; }
}
