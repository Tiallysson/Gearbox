using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.UI;

/// <summary>Um disco na aba "Armazenamento", com os textos já prontos para o template.</summary>
public sealed class DriveItem(DriveUsage usage)
{
    public string Root => usage.Root;

    public string Letter => usage.Letter;

    public double Percent => usage.UsedPercent;

    public string Glyph => usage.IsRemovable ? NotchGlyphs.Usb : NotchGlyphs.HardDrive;

    public string Caption => ModuleStrings.Format("DriveFreeFormat", ByteFormat.Format(usage.FreeBytes));

    public string ToolTip => ModuleStrings.Format(
        "DriveToolTipFormat",
        string.IsNullOrWhiteSpace(usage.Label) ? usage.Letter : $"{usage.Label} ({usage.Letter})",
        ByteFormat.Format(usage.TotalBytes - usage.FreeBytes),
        ByteFormat.Format(usage.TotalBytes));
}
