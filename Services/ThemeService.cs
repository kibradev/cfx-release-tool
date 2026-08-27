using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ReleaseTool.Desktop.Services;

public static class ThemeService
{
    /// <summary>Kullanıcı seçebileceği hazır accent renkleri (ad → hex).</summary>
    public static readonly (string Name, string Hex)[] AccentPresets =
    [
        ("Emerald", "#059669"),
        ("Blue", "#2563EB"),
        ("Violet", "#7C3AED"),
        ("Rose", "#E11D48"),
        ("Amber", "#D97706"),
        ("Cyan", "#0891B2"),
    ];

    public static void Apply(bool dark, string? accentHex = null)
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

        if (!string.IsNullOrWhiteSpace(accentHex))
        {
            try { Set("AccentBrush", accentHex); }
            catch { /* geçersiz hex — varsayılanı koru */ }
        }
    }

    /// <summary>Windows kişiselleştirme ayarından koyu tema kullanılıyor mu?</summary>
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i == 0;
        }
        catch
        {
            // registry okunamadı
        }
        return false;
    }
}
