using System.Windows;
using System.Windows.Media;

namespace ReleaseTool.Desktop.Services;

public static class ThemeService
{
    public static void Apply(bool dark)
    {
        var app = Application.Current;
        if (app == null)
            return;

        void Set(string key, string color) =>
            app.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);

        if (dark)
        {
            Set("BgBrush", "#0C0A09");
            Set("PanelBrush", "#1C1917");
            Set("BorderBrush", "#44403C");
            Set("TextPrimaryBrush", "#FAFAF9");
            Set("TextSecondaryBrush", "#A8A29E");
            Set("AccentBrush", "#34D399");
            Set("TreeBgBrush", "#292524");
            Set("HoverBrush", "#44403C");
            Set("VersionBgBrush", "#064E3B");
            Set("VersionBorderBrush", "#047857");
            Set("VersionTextBrush", "#6EE7B7");
        }
        else
        {
            Set("BgBrush", "#F5F5F4");
            Set("PanelBrush", "#FFFFFF");
            Set("BorderBrush", "#E7E5E4");
            Set("TextPrimaryBrush", "#1C1917");
            Set("TextSecondaryBrush", "#78716C");
            Set("AccentBrush", "#059669");
            Set("TreeBgBrush", "#FAFAF9");
            Set("HoverBrush", "#FAFAF9");
            Set("VersionBgBrush", "#ECFDF5");
            Set("VersionBorderBrush", "#A7F3D0");
            Set("VersionTextBrush", "#047857");
        }
    }
}
