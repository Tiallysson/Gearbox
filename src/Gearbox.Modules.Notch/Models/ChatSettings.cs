
namespace Gearbox.Modules.Notch.Models;

public sealed class ChatSettings
{
    public const string SettingsId = "NotchChat";

    public string Model { get; set; } = ChatConversation.DefaultModel;

    public decimal KeyCreditLimit { get; set; }

    public bool AllowPaidModels { get; set; }

    public bool AllowDataCollection { get; set; }
}
