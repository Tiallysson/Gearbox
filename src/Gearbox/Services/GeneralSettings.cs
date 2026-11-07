namespace Gearbox.Services;

public sealed class GeneralSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool CheckForUpdates { get; set; } = true;

    public bool IncludePrereleases { get; set; }
}
