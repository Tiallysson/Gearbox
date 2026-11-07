using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class LinkRow(QuickLink link) : ObservableObject
{
    public string Id => link.Id;

    public string Name => link.Name;

    public string Detail => link.Kind == QuickLinkKind.Web ? link.Target : ModuleStrings.Get("LinkAppDetail");

    public string Glyph => link.Kind == QuickLinkKind.Web ? "" : "";

    public string MoveUpLabel => ModuleStrings.Format("LinkMoveUpFormat", link.Name);

    public string MoveDownLabel => ModuleStrings.Format("LinkMoveDownFormat", link.Name);

    public string RemoveLabel => ModuleStrings.Format("LinkRemoveFormat", link.Name);

    [ObservableProperty]
    public partial ImageSource? Icon { get; set; }
}
