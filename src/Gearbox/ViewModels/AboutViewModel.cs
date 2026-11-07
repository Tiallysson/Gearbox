using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using Gearbox.Core;
using Gearbox.Services;

namespace Gearbox.ViewModels;

public sealed partial class AboutViewModel(GearboxPaths paths)
{
    public string Version => AppInfo.Version;

    public string Runtime { get; } = $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})";

    public string DataFolder => paths.RootDirectory;

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(paths.RootDirectory);
        Process.Start(new ProcessStartInfo { FileName = paths.RootDirectory, UseShellExecute = true });
    }
}
