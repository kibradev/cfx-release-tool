using System.Text.RegularExpressions;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class ManifestLintService
{
    public static List<string> Lint(string content, string manifestFile)
    {
        var issues = new List<string>();

        if (!Regex.IsMatch(content, @"^\s*fx_version\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            issues.Add("fx_version satırı eksik");

        if (!Regex.IsMatch(content, @"^\s*game\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            issues.Add("game satırı eksik");

        if (!Regex.IsMatch(content, @"^\s*version\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            issues.Add("version satırı eksik (release sırasında otomatik eklenir)");

        if (Regex.IsMatch(content, @"client_script\s*\{\s*\}", RegexOptions.IgnoreCase))
            issues.Add("Boş client_scripts bloğu");

        if (Regex.IsMatch(content, @"server_script\s*\{\s*\}", RegexOptions.IgnoreCase))
            issues.Add("Boş server_scripts bloğu");

        if (content.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            issues.Add("Manifest içinde TODO ifadesi var");

        if (manifestFile.Equals("__resource.lua", StringComparison.OrdinalIgnoreCase))
            issues.Add("Eski __resource.lua kullanılıyor — fxmanifest.lua önerilir");

        return issues;
    }
}
