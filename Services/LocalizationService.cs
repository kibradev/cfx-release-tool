using System.ComponentModel;

namespace ReleaseTool.Desktop.Services;

public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string _lang = AppServices.Config.Load().Language;

    private static readonly Dictionary<string, (string Tr, string En)> Strings = new()
    {
        ["app_title"] = ("FiveM Release Aracı", "FiveM Release Tool"),
        ["header_kicker"] = ("FIVEM RELEASE", "FIVEM RELEASE"),
        ["header_title"] = ("Resource release aracı", "Resource release tool"),
        ["header_subtitle"] = ("Klasörü sürükle-bırak veya seç · Escrow dosyalarını işaretle · ZIP'leri kaydet",
                               "Drag & drop or pick a folder · Mark escrow files · Save ZIPs"),
        ["release_create"] = ("Release oluştur", "Create release"),
        ["release_cancel"] = ("İptal", "Cancel"),
        ["bump_version"] = ("Sürüm artır", "Bump version"),
        ["bump_on_release"] = ("Release'te sürüm artır", "Bump version on release"),
        ["settings"] = ("Ayarlar", "Settings"),
        ["portal_settings"] = ("Portal ayarları", "Portal settings"),
        ["portal_upload"] = ("Portal'a yükle", "Upload to Portal"),
        ["portal"] = ("Portal", "Portal"),
        ["theme"] = ("Tema", "Theme"),
        ["tools"] = ("Araçlar", "Tools"),
        ["busy"] = ("Meşgul…", "Busy…"),
        ["done"] = ("Tamamlandı", "Done"),
        ["resources"] = ("RESOURCES", "RESOURCES"),
        ["pick_folder"] = ("Klasör seç…", "Pick folder…"),
        ["watch_folder"] = ("Klasörü izle", "Watch folder"),
        ["scripts"] = ("Scriptler", "Scripts"),
        ["all"] = ("Tümü", "All"),
        ["clear"] = ("Temizle", "Clear"),
        ["output_folder"] = ("ÇIKTI KLASÖRÜ", "OUTPUT FOLDER"),
        ["output_pick"] = ("Çıktı klasörü…", "Output folder…"),
        ["history"] = ("GEÇMİŞ", "HISTORY"),
        ["no_history"] = ("Henüz release yok", "No releases yet"),
        ["preview_escrow"] = ("Escrow özeti", "Escrow summary"),
        ["edit_manifest"] = ("fxmanifest", "fxmanifest"),
        ["edit_changelog"] = ("CHANGELOG", "CHANGELOG"),
        ["compare_release"] = ("Önceki ile karşılaştır", "Compare with previous"),
        ["export_profile"] = ("Profil dışa", "Export profile"),
        ["import_profile"] = ("Profil içe", "Import profile"),
        ["lint_manifest"] = ("Manifest lint", "Manifest lint"),
        ["verify_zip"] = ("ZIP doğrula", "Verify ZIP"),
        ["security_scan"] = ("Güvenlik taraması", "Security scan"),
        ["preflight"] = ("Ön kontrol", "Preflight check"),
        ["refresh_portal"] = ("Portal oturum", "Refresh portal session"),
        ["manifest_preview"] = ("Manifest önizle", "Preview manifest"),
        ["release_ignore"] = ("release.ignore", "release.ignore"),
        ["recent"] = ("Son kullanılan", "Recent"),
        ["folders"] = ("Klasörler", "Folders"),
        ["progress"] = ("İlerleme", "Progress"),
        ["manual_version"] = ("Manuel sürüm", "Manual version"),
        ["watch_on"] = ("İzleme: açık", "Watch: on"),
        ["watch_off"] = ("İzleme: kapalı", "Watch: off"),
        ["escrow_files"] = ("Escrow dosyaları", "Escrow files"),
        ["selected"] = ("seçili", "selected"),
        ["all_lua"] = ("Tüm .lua", "All .lua"),
        ["select_all"] = ("Tümünü seç", "Select all"),
        ["only_auto"] = ("Sadece otomatik", "Auto only"),
        ["filter_tree"] = ("Dosya ağacında ara (config, shared…)", "Search files (config, shared…)"),
        ["empty_hint"] = ("Resources klasörünü seçin veya buraya sürükleyip bırakın",
                          "Pick a resources folder or drop it here"),
        ["reading_files"] = ("Dosyalar okunuyor…", "Reading files…"),
        ["preset"] = ("Preset", "Preset"),
        ["preset_save"] = ("Preset kaydet", "Save preset"),
        ["batch_release"] = ("Toplu release", "Batch release"),
    };

    public string this[string key] => Get(key);

    public static string Get(string key)
    {
        if (!Strings.TryGetValue(key, out var pair))
            return key;
        return _lang.Equals("en", StringComparison.OrdinalIgnoreCase) ? pair.En : pair.Tr;
    }

    public static string CurrentLanguage => _lang;

    /// <summary>Dili değiştir ve tüm bağlı metinleri canlı güncelle.</summary>
    public void SetLanguage(string lang)
    {
        _lang = string.IsNullOrWhiteSpace(lang) ? "tr" : lang;
        Refresh();
    }

    public void Refresh() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}

public static class AppServices
{
    public static ConfigService Config { get; } = new();
    public static ReleaseOrchestrator Orchestrator { get; } = new();
    public static FolderWatchService FolderWatch { get; } = new();
}
