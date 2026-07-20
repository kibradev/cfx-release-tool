namespace ReleaseTool.Desktop.Services;

public static class Loc
{
    private static readonly Dictionary<string, (string Tr, string En)> Strings = new()
    {
        ["app_title"] = ("FiveM Release Aracı", "FiveM Release Tool"),
        ["release_create"] = ("Release oluştur", "Create release"),
        ["settings"] = ("Ayarlar", "Settings"),
        ["tools"] = ("Araçlar", "Tools"),
        ["busy"] = ("Meşgul…", "Busy…"),
        ["done"] = ("Tamamlandı", "Done"),
        ["preview_escrow"] = ("Escrow özeti", "Escrow summary"),
        ["edit_manifest"] = ("fxmanifest", "fxmanifest"),
        ["edit_changelog"] = ("CHANGELOG", "CHANGELOG"),
        ["compare_release"] = ("Önceki ile karşılaştır", "Compare with previous"),
        ["export_profile"] = ("Profil dışa aktar", "Export profile"),
        ["import_profile"] = ("Profil içe aktar", "Import profile"),
        ["lint_manifest"] = ("Manifest lint", "Manifest lint"),
        ["verify_zip"] = ("ZIP doğrula", "Verify ZIP"),
        ["refresh_portal"] = ("Portal oturumu yenile", "Refresh portal session"),
        ["recent"] = ("Son kullanılan", "Recent"),
        ["folders"] = ("Klasörler", "Folders"),
        ["progress"] = ("İlerleme", "Progress"),
        ["manual_version"] = ("Manuel sürüm", "Manual version"),
        ["watch_on"] = ("İzleme: açık", "Watch: on"),
        ["watch_off"] = ("İzleme: kapalı", "Watch: off"),
    };

    public static string Get(string key)
    {
        var lang = AppServices.Config.Load().Language;
        if (!Strings.TryGetValue(key, out var pair))
            return key;
        return lang.Equals("en", StringComparison.OrdinalIgnoreCase) ? pair.En : pair.Tr;
    }
}

public static class AppServices
{
    public static ConfigService Config { get; } = new();
    public static ReleaseOrchestrator Orchestrator { get; } = new();
    public static FolderWatchService FolderWatch { get; } = new();
}
