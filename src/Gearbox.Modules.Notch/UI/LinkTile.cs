using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class LinkTile : ObservableObject
{
    private const string GlobeGlyph = "";
    private const string AppGlyph = "";
    private const string PlusGlyph = "";

    private LinkTile(string id, string name, string toolTip, string glyph)
    {
        Id = id;
        Name = name;
        ToolTip = toolTip;
        Glyph = glyph;
    }

    public string Id { get; }

    public string Name { get; }

    public string ToolTip { get; }

    public bool IsAdd => Id.Length == 0;

    [ObservableProperty]
    public partial string Glyph { get; set; }

    [ObservableProperty]
    public partial ImageSource? Icon { get; set; }

    internal static LinkTile For(QuickLink link) => new(
        link.Id,
        link.Name,
        link.Kind == QuickLinkKind.Web ? ModuleStrings.Format("LinkOpenWebFormat", link.Name, link.Target) : ModuleStrings.Format("LinkOpenAppFormat", link.Name),
        link.Kind == QuickLinkKind.Web ? GlobeGlyph : AppGlyph);

    internal static LinkTile Add() => new(string.Empty, ModuleStrings.Get("LinkAdd"), ModuleStrings.Get("LinkAddToolTip"), PlusGlyph);

    internal void ShowIcon(ImageSource icon)
    {
        Icon = icon;
        Glyph = string.Empty;
    }
}
