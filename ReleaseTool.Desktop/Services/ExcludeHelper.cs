using System.Text.RegularExpressions;
using ReleaseTool.Desktop.Helpers;

namespace ReleaseTool.Desktop.Services;

public static class ExcludeHelper
{
    public static bool ShouldExclude(string fileNameOrPath, IEnumerable<string> excludeList)
    {
        var normalized = PathHelper.ToPosix(fileNameOrPath);
        foreach (var exclude in excludeList)
        {
            var ex = PathHelper.ToPosix(exclude);
            if (ex.Contains('*'))
            {
                var pattern = "^" + Regex.Escape(ex).Replace("\\*", ".*") + "$";
                if (Regex.IsMatch(normalized, pattern))
                    return true;
            }
            else if (normalized == ex || normalized.EndsWith("/" + ex, StringComparison.Ordinal) ||
                     normalized.StartsWith(ex + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
