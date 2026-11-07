using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Modules.Wallpaper.Logic.Gallery;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Wallpaper.UI;

public sealed partial class GalleryCardViewModel(GalleryItem item) : ObservableObject
{
    internal GalleryItem Item => item;

    public string Id => item.Id;

    public string Name => ModuleStrings.Get($"Gallery_{Key}_Name");

    public string Description => ModuleStrings.Get($"Gallery_{Key}_Description");

    public string KindLabel => ModuleStrings.Get(item.IsInteractive ? "GalleryKindInteractive" : "GalleryKindStatic");

    public bool IsInteractive => item.IsInteractive;

    public bool IsStatic => !item.IsInteractive;

    public string SceneId => item.IsInteractive ? item.Id : string.Empty;

    public string AutomationName => ModuleStrings.Format("GalleryCardAutomationFormat", Name, KindLabel);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInactive))]
    public partial bool IsActive { get; set; }

    public bool IsInactive => !IsActive;

    [ObservableProperty]
    public partial bool IsHovered { get; set; }

    [ObservableProperty]
    public partial ImageSource? Thumbnail { get; set; }

    [ObservableProperty]
    public partial ImageSource? Preview { get; set; }

    public override string ToString() => AutomationName;

    private string Key => item.Id.Replace("-", string.Empty, StringComparison.Ordinal);
}
