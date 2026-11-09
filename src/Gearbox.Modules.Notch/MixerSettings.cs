using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch;

public sealed class MixerSettings
{
    public const string SettingsId = "NotchMixer";

    public Dictionary<string, MixerAppPreference?> Apps { get; set; } = [];
}
