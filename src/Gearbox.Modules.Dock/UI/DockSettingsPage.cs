using Gearbox.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace Gearbox.Modules.Dock.UI;

public sealed partial class DockSettingsPage : Page
{
    public DockSettingsPage(ISettingsService<DockSettings> settings)
    {
        var value = settings.Load();
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = ModuleStrings.Get("SettingsIntro"), TextWrapping = TextWrapping.Wrap });
        AddToggle("DarkTheme", value.DarkTheme, v => value.DarkTheme = v);
        AddToggle("ShowClock", value.ShowClock, v => value.ShowClock = v);
        AddToggle("ShowFocus", value.ShowFocus, v => value.ShowFocus = v);
        AddSlider("IconSize", value.IconSize, 32, 56, v => value.IconSize = v);
        AddSlider("BottomMargin", value.BottomMargin, 0, 64, v => value.BottomMargin = v);
        Content = panel;

        void AddToggle(string key, bool initial, Action<bool> set)
        {
            var toggle = new ToggleSwitch { Header = ModuleStrings.Get(key), IsOn = initial };
            toggle.Toggled += (_, _) => { set(toggle.IsOn); settings.Save(value); };
            panel.Children.Add(toggle);
        }
        void AddSlider(string key, int initial, int min, int max, Action<int> set)
        {
            panel.Children.Add(new TextBlock { Text = ModuleStrings.Get(key) });
            var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = 1, Value = Math.Clamp(initial, min, max) };
            AutomationProperties.SetName(slider, ModuleStrings.Get(key));
            slider.ValueChanged += (_, _) => { set((int)slider.Value); settings.Save(value); };
            panel.Children.Add(slider);
        }
    }
}
