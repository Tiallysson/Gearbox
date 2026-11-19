
namespace Gearbox.Modules.Notch.Models;

public sealed class LinksSettings
{
    public const string SettingsId = "NotchLinks";

    public List<QuickLink> Links { get; set; } = [];
}
