using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch;

public sealed class LinksSettings
{
    public const string SettingsId = "NotchLinks";

    public List<QuickLink> Links { get; set; } = [];
}
