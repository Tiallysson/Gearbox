namespace Gearbox.Modules.Notch.UI;

public readonly record struct MixerVolumeRequest(string Key, double Percent);

public readonly record struct MixerOutputRequest(string Key, string? DeviceId);
