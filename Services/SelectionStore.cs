using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReleaseTool.Desktop.Services;

public sealed class SelectionStore
{
    private readonly string _dir;

    public SelectionStore()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReleaseTool", "selections");
        Directory.CreateDirectory(_dir);
    }

    public List<string>? Load(string resourcePath)
    {
        try
        {
            var file = GetFilePath(resourcePath);
            if (!File.Exists(file))
                return null;

            var json = File.ReadAllText(file);
            var data = JsonSerializer.Deserialize<SelectionData>(json);
            return data?.Paths;
        }
        catch
        {
            return null;
        }
    }

    public void Save(string resourcePath, IEnumerable<string> paths)
    {
        try
        {
            var data = new SelectionData
            {
                ResourcePath = resourcePath,
                Paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                SavedAt = DateTime.Now
            };
            File.WriteAllText(GetFilePath(resourcePath), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // sessiz
        }
    }

    private string GetFilePath(string resourcePath)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(resourcePath).ToLowerInvariant())))[..16];
        return Path.Combine(_dir, $"{hash}.json");
    }

    private sealed class SelectionData
    {
        public string ResourcePath { get; set; } = "";
        public List<string> Paths { get; set; } = [];
        public DateTime SavedAt { get; set; }
    }
}
