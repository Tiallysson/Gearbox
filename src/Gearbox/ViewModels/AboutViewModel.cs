using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using Gearbox.Core;

namespace Gearbox.ViewModels;

public sealed partial class AboutViewModel(GearboxPaths paths)
{
    public string Version { get; } =
        typeof(AboutViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(AboutViewModel).Assembly.GetName().Version?.ToString()
        ?? "?";

    public string Runtime { get; } = $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})";

    public string DataFolder => paths.RootDirectory;

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(paths.RootDirectory);
        Process.Start(new ProcessStartInfo { FileName = paths.RootDirectory, UseShellExecute = true });
    }
}
