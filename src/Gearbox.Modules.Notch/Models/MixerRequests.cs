namespace Gearbox.Modules.Notch.Models;

public readonly record struct MixerVolumeRequest(string Key, double Percent);

public readonly record struct MixerOutputRequest(string Key, string? DeviceId);
