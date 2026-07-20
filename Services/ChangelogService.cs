namespace ReleaseTool.Desktop.Services;

public static class ChangelogService
{
    public static void UpdateChangelog(string resourcePath, string newVersion)
    {
        try
        {
            var changelogPath = Path.Combine(resourcePath, "CHANGELOG.md");
            var content = File.Exists(changelogPath)
                ? File.ReadAllText(changelogPath)
                : "# Changelog\n\n";

            var now = DateTime.Now;
            var date = $"{now:yyyy-MM-dd HH:mm}";
            var entry = $"## v{newVersion}\nDate: {date}\n\n- Automated Release\n\n";
            File.WriteAllText(changelogPath, entry + content);
        }
        catch
        {
            // opsiyonel — sessiz
        }
    }
}
