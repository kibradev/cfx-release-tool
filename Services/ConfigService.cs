using System.Text.Json;
using System.Text.Json.Serialization;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _appDataConfigPath;
    private readonly string _localConfigPath;

    public ConfigService()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReleaseTool");
        Directory.CreateDirectory(appDataDir);
        _appDataConfigPath = Path.Combine(appDataDir, "config.json");

        var exeDir = AppContext.BaseDirectory;
        _localConfigPath = Path.Combine(exeDir, "config.json");
    }

    public string ConfigPath =>
        File.Exists(_appDataConfigPath) ? _appDataConfigPath : _localConfigPath;

    public AppConfig Load()
    {
        var path = ConfigPath;
        if (!File.Exists(path))
        {
            var example = Path.Combine(AppContext.BaseDirectory, "config.example.json");
            if (File.Exists(example))
            {
                File.Copy(example, path, overwrite: false);
            }
            else
            {
                Save(new AppConfig());
            }
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            config.Exclude ??= [];
            config.EscrowZipExtraExcludes ??= [];
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var path = File.Exists(_appDataConfigPath) || !File.Exists(_localConfigPath)
            ? _appDataConfigPath
            : _localConfigPath;

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }

    public string GetOutputFolder(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.OutputFolder))
        {
            var expanded = ExpandEnv(config.OutputFolder);
            try
            {
                Directory.CreateDirectory(expanded);
                return Path.GetFullPath(expanded);
            }
            catch
            {
                // fallback
            }
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ReleaseTool", "releases");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    public static string ExpandEnv(string value)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return value
            .Replace("%USERPROFILE%", home, StringComparison.OrdinalIgnoreCase)
            .Replace("%HOME%", home, StringComparison.OrdinalIgnoreCase)
            .Replace("$HOME", home, StringComparison.Ordinal);
    }
}
