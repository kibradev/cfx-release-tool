using ReleaseTool.Desktop.Helpers;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class EscrowIgnoreService
{
    private static readonly string[] DefaultAutoEscrowDirs =
    [
        "escrow", "shared", "config", "configs", "editable",
        "settings", "locales", "locale", "configuration"
    ];

    /// <summary>Bu klasörlerdeki tüm dosyalar (uzantı fark etmez) otomatik seçilir.</summary>
    private static readonly string[] DefaultAllExtensionsDirs = ["editable"];

    private static readonly string[] DefaultAutoEscrowFileNames =
    [
        "config.json", "settings.json", "config.cfg", "config.xml",
        "shared.lua", "config.lua", "settings.lua", "locale.lua"
    ];

    private static readonly string[] DefaultAutoEscrowStems =
    [
        "escrow", "shared", "config", "settings", "locale", "locales", "permissions"
    ];

    private static readonly string[] ConfigLikeKeywords = ["config", "shared", "settings", "locale"];

    public static bool EscrowPatternMatchesPath(string filePath, string pattern)
    {
        var normalized = PathHelper.ToPosix(filePath);
        var ex = PathHelper.ToPosix(pattern).TrimStart('.', '/');

        if (ex.Contains('*'))
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(ex)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(normalized, regex);
        }

        if (normalized == ex)
            return true;

        return normalized.StartsWith(ex + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// FiveM'de sık görülen config/shared/escrow/editable yollarını otomatik tanır.
    /// Kullanıcı checkbox'tan kaldırabilir.
    /// </summary>
    public static bool IsAutoEscrowIgnoreFile(string filePath, AppConfig? config = null)
    {
        var posix = PathHelper.ToPosix(filePath);
        var parts = posix.Split('/');
        var fileName = parts[^1];
        var lowerName = fileName.ToLowerInvariant();

        var autoDirs = Merge(DefaultAutoEscrowDirs, config?.AutoEscrowDirs);
        var allExtDirs = Merge(DefaultAllExtensionsDirs, config?.AutoEscrowAllExtensionsDirs);
        var autoFileNames = Merge(DefaultAutoEscrowFileNames, config?.AutoEscrowFileNames);
        var autoStems = Merge(DefaultAutoEscrowStems, config?.AutoEscrowFileStems);

        if (autoFileNames.Contains(lowerName))
            return true;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (allExtDirs.Contains(parts[i]))
                return true;
        }

        if (IsConfigDataFile(lowerName) && IsUnderAutoDir(parts, autoDirs))
            return true;

        if (!lowerName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            return false;

        var stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

        if (autoStems.Contains(stem) || IsConfigLikeStem(stem))
            return true;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (autoDirs.Contains(parts[i]))
                return true;
        }

        return false;
    }

    public static HashSet<string> ExpandEscrowIgnoreToFiles(IEnumerable<string> patterns, IEnumerable<string> filePaths)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var patternList = patterns.ToList();
        if (patternList.Count == 0)
            return selected;

        foreach (var file in filePaths)
        {
            foreach (var pattern in patternList)
            {
                if (EscrowPatternMatchesPath(file, pattern))
                {
                    selected.Add(file);
                    break;
                }
            }
        }

        return selected;
    }

    public static HashSet<string> BuildInitialEscrowSelection(
        IEnumerable<string> patterns,
        IEnumerable<string> filePaths,
        AppConfig? config = null)
    {
        var fileList = filePaths.ToList();
        var selected = ExpandEscrowIgnoreToFiles(patterns, fileList);

        foreach (var file in fileList)
        {
            if (IsAutoEscrowIgnoreFile(file, config))
                selected.Add(file);
        }

        return selected;
    }

    public static HashSet<string> GetAutoSuggestedPaths(IEnumerable<string> filePaths, AppConfig? config = null)
    {
        return filePaths
            .Where(f => IsAutoEscrowIgnoreFile(f, config))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsConfigDataFile(string lowerName)
    {
        return lowerName.EndsWith(".json", StringComparison.Ordinal)
               || lowerName.EndsWith(".cfg", StringComparison.Ordinal)
               || lowerName.EndsWith(".xml", StringComparison.Ordinal)
               || lowerName.EndsWith(".ini", StringComparison.Ordinal);
    }

    private static bool IsUnderAutoDir(string[] parts, HashSet<string> autoDirs)
    {
        if (parts.Length == 1)
            return false;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (autoDirs.Contains(parts[i]))
                return true;
        }

        return false;
    }

    private static bool IsConfigLikeStem(string stem)
    {
        foreach (var keyword in ConfigLikeKeywords)
        {
            if (stem == keyword
                || stem.EndsWith($"_{keyword}", StringComparison.Ordinal)
                || stem.StartsWith($"{keyword}_", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> Merge(string[] defaults, List<string>? custom)
    {
        var set = new HashSet<string>(defaults, StringComparer.OrdinalIgnoreCase);
        if (custom == null)
            return set;

        foreach (var item in custom.Where(x => !string.IsNullOrWhiteSpace(x)))
            set.Add(item.Trim());

        return set;
    }
}
