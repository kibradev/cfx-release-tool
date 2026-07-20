using System.Text.Json;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public sealed class ReleaseHistoryService
{
    private readonly string _filePath;
    private const int MaxEntries = 100;

    public ReleaseHistoryService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReleaseTool");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "history.json");
    }

    public List<ReleaseHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return [];

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<ReleaseHistoryEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Add(ReleaseHistoryEntry entry)
    {
        var list = Load();
        list.Insert(0, entry);
        if (list.Count > MaxEntries)
            list = list.Take(MaxEntries).ToList();

        File.WriteAllText(_filePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }
}
