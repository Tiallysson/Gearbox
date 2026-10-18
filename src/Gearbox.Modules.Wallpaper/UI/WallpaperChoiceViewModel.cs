using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gearbox.Modules.Wallpaper.Logic;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Gearbox.Modules.Wallpaper.UI;

/// <summary>Um arquivo escolhido: o geral ("todos os monitores") ou o de um monitor.</summary>
public sealed partial class WallpaperChoiceViewModel : ObservableObject
{
    private readonly Func<Task<string?>> _pickFile;
    private readonly Action _changed;

    public WallpaperChoiceViewModel(string? devicePath, string title, string? path, Func<Task<string?>> pickFile, Action changed)
    {
        DevicePath = devicePath;
        Title = title;
        _pickFile = pickFile;
        _changed = changed;
        Path = path;
    }

    /// <summary><c>null</c> = escolha geral.</summary>
    public string? DevicePath { get; }

    public string Title { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName), nameof(HasFile), nameof(KindGlyph), nameof(Thumbnail), nameof(HasThumbnail), nameof(HasGlyph))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    public partial string? Path { get; set; }

    public bool HasFile => !string.IsNullOrWhiteSpace(Path);

    public string FileName => HasFile ? System.IO.Path.GetFileName(Path!) : ModuleStrings.Get("NoFileChosen");

    /// <summary>Miniatura das imagens (vídeos mostram um ícone).</summary>
    public ImageSource? Thumbnail { get; private set; }

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasGlyph => !HasThumbnail;

    public string KindGlyph => MediaKinds.Detect(Path) switch
    {
        MediaKind.Video => "", // Video
        MediaKind.Image => "", // Photo2
        _ => "", // Photo (wallpaper do Gearbox)
    };

    partial void OnPathChanged(string? value)
    {
        Thumbnail = HasFile && MediaKinds.Detect(value) == MediaKind.Image && File.Exists(value)
            ? new BitmapImage(new Uri(value!)) { DecodePixelWidth = 192 }
            : null;
        _changed();
    }

    [RelayCommand]
    private async Task PickAsync()
    {
        var path = await _pickFile();
        if (path is not null)
        {
            Path = path;
        }
    }

    private bool CanClear() => HasFile;

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear() => Path = null;
}
