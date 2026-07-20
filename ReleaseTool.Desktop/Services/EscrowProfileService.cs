using System.Text.Json;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public sealed class EscrowProfileService
{
    private readonly string _dir;

    public EscrowProfileService()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReleaseTool", "profiles");
        Directory.CreateDirectory(_dir);
    }

    public void Export(string profileName, IEnumerable<string> paths)
    {
        var file = Path.Combine(_dir, Sanitize(profileName) + ".json");
        var data = new { name = profileName, paths = paths.ToList(), exportedAt = DateTime.UtcNow };
        File.WriteAllText(file, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    public List<string> Import(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("paths", out var arr))
            return [];

        return arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(p => p.Length > 0).ToList();
    }

    public List<string> ListProfiles()
    {
        return Directory.GetFiles(_dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Cast<string>()
            .OrderBy(x => x)
            .ToList();
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')).Trim();
}
