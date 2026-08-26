using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString() == "invert";
        var visible = value != null && (value is not string s || !string.IsNullOrEmpty(s));
        if (invert)
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString() == "invert";
        var visible = value is true;
        if (invert)
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BytesToMegabytesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        if (value is int i)
            return $"{i / (1024.0 * 1024.0):F2} MB";
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ModeToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() == "escrow" ? "Escrow" : "OS";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dosya uzantısına göre küçük bir emoji ikon döndürür (dosya ağacı için).</summary>
public sealed class PathToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value?.ToString() ?? "";
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var name = System.IO.Path.GetFileName(path).ToLowerInvariant();

        if (name is "fxmanifest.lua" or "__resource.lua")
            return "📜";

        return ext switch
        {
            ".lua" => "🌙",
            ".js" or ".ts" or ".jsx" or ".tsx" => "🟨",
            ".json" => "🔧",
            ".cfg" or ".ini" or ".toml" or ".yml" or ".yaml" => "⚙️",
            ".html" or ".htm" => "🌐",
            ".css" or ".scss" or ".sass" => "🎨",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".ico" => "🖼️",
            ".ogg" or ".mp3" or ".wav" => "🔊",
            ".ytd" or ".yft" or ".ydr" or ".ydd" or ".ymap" or ".ytyp" => "📦",
            ".sql" => "🗄️",
            ".md" or ".txt" => "📄",
            ".dll" or ".exe" => "🧩",
            _ => "📄"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Toast türüne göre kenar/vurgu rengi.</summary>
public sealed class ToastKindToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is ToastKind kind
            ? kind switch
            {
                ToastKind.Success => "#059669",
                ToastKind.Warning => "#D97706",
                ToastKind.Error => "#DC2626",
                _ => "#2563EB"
            }
            : "#2563EB";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
