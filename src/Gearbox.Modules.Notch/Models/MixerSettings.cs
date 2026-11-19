
namespace Gearbox.Modules.Notch.Models;

public sealed class MixerSettings
{
    public const string SettingsId = "NotchMixer";

    public Dictionary<string, MixerAppPreference?> Apps { get; set; } = [];
}
