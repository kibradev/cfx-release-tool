using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReleaseTool.Desktop.Models;
using ReleaseTool.Desktop.Services;
using ReleaseTool.Desktop.Views;
using LocSvc = ReleaseTool.Desktop.Services.Loc;

namespace ReleaseTool.Desktop.ViewModels;

public partial class MainViewModel
{
    partial void OnProgressPercentChanged(int value) =>
        OnPropertyChanged(nameof(ShowProgress));

    partial void OnWatchEnabledChanged(bool value)
    {
        var config = _configService.Load();
        config.WatchResourcesFolder = value;
        _configService.Save(config);

        if (value && !string.IsNullOrWhiteSpace(ResourcesFolder))
            AppServices.FolderWatch.Start(ResourcesFolder, () =>
                Application.Current.Dispatcher.Invoke(RefreshResourcesList));
        else
            AppServices.FolderWatch.Stop();
    }

    partial void OnSelectedResourcesFolderChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value) &&
            !string.Equals(ResourcesFolder, value, StringComparison.OrdinalIgnoreCase))
        {
            ResourcesFolder = value;
            SaveConfig();
            RefreshResourcesList();
        }
    }

    private async Task CheckUpdatesOnStartupAsync()
    {
        var config = _configService.Load();
        if (!config.CheckForUpdates)
            return;

        var (available, msg) = await AutoUpdateService.CheckAsync();
        if (available && msg != null)
        {
            var r = MessageBox.Show(msg + "\n\nİndirme sayfasını aç?", "Güncelleme", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (r == MessageBoxResult.Yes)
                AutoUpdateService.OpenDownloadPage();
        }
    }

    [RelayCommand]
    private void OpenAppSettings()
    {
        var dlg = new AppSettingsDialog { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            LoadConfig();
    }

    [RelayCommand]
    private void RefreshPortalSession()
    {
        var win = new PortalLoginWindow { Owner = Application.Current.MainWindow };
        if (win.ShowDialog() == true)
            StatusText = "Portal oturumu yenilendi";
    }

    [RelayCommand]
    private void PreviewEscrowSummary()
    {
        if (_selectedResource == null)
            return;

        var config = _configService.Load();
        var summary = ReleasePreviewService.Build(_selectedResource.Path, GetSelectedEscrowPaths(), config);
        TextEditorDialog.ShowPreview(LocSvc.Get("preview_escrow"), ReleasePreviewService.FormatSummary(summary));
    }

    [RelayCommand]
    private void EditFxManifest()
    {
        if (_selectedResource == null || string.IsNullOrEmpty(ManifestFileName))
            return;

        var path = Path.Combine(_selectedResource.Path, ManifestFileName);
        var content = File.ReadAllText(path);
        if (TextEditorDialog.ShowEdit(LocSvc.Get("edit_manifest"), ref content) != true)
            return;

        File.WriteAllText(path, content);
        _manifestContent = content;
        StatusText = "Manifest kaydedildi";
        _ = LoadResourceDetailsAsync(_selectedResource);
    }

    [RelayCommand]
    private void EditChangelog()
    {
        if (_selectedResource == null)
            return;

        var path = Path.Combine(_selectedResource.Path, "CHANGELOG.md");
        var content = File.Exists(path)
            ? File.ReadAllText(path)
            : "# Changelog\n\n## v1.0.0\n- Initial release\n";

        if (TextEditorDialog.ShowEdit(LocSvc.Get("edit_changelog"), ref content) != true)
            return;

        File.WriteAllText(path, content);
        StatusText = "CHANGELOG kaydedildi";
    }

    [RelayCommand]
    private void LintManifest()
    {
        if (_selectedResource == null)
            return;

        var issues = ManifestLintService.Lint(_manifestContent, ManifestFileName);
        var analysis = ManifestAnalyzerService.Analyze(_selectedResource.Path, _manifestContent, _configService.Load());
        var warnings = BuildWarningsText(analysis);
        var all = issues.Concat(warnings.Split('\n', StringSplitOptions.RemoveEmptyEntries)).Distinct().ToList();
        var text = all.Count == 0 ? "Sorun bulunamadı ✓" : string.Join(Environment.NewLine, all.Select(i => "- " + i));
        TextEditorDialog.ShowPreview("Manifest lint", text);
    }

    [RelayCommand]
    private void VerifyLastZips()
    {
        if (LastZips.Count == 0)
            return;

        var lines = LastZips.Select(z =>
        {
            var (ok, msg) = ZipVerifyService.Verify(z.ZipPath);
            return $"{z.ZipName}: {(ok ? "OK" : "HATA")} — {msg}";
        });
        TextEditorDialog.ShowPreview(LocSvc.Get("verify_zip"), string.Join(Environment.NewLine, lines));
    }

    [RelayCommand]
    private void CompareWithPreviousRelease()
    {
        if (_selectedResource == null || LastZips.Count == 0)
            return;

        var escrow = LastZips.FirstOrDefault(z => z.Mode == "escrow");
        if (escrow == null)
            return;

        var prev = _orchestrator.FindPreviousEscrowZip(_selectedResource.Name, escrow.ZipPath);
        var diff = ReleaseDiffService.CompareWithPrevious(prev, escrow.ZipPath);
        TextEditorDialog.ShowPreview(LocSvc.Get("compare_release"), diff);
    }

    [RelayCommand]
    private void ExportEscrowProfile()
    {
        if (_selectedResource == null)
            return;

        var name = _selectedResource.Name;
        var dlg = new SaveFileDialog
        {
            Filter = "JSON|*.json",
            FileName = $"{name}-escrow-profile.json"
        };
        if (dlg.ShowDialog() != true)
            return;

        var paths = GetSelectedEscrowPaths();
        var json = System.Text.Json.JsonSerializer.Serialize(new { name, paths = paths.ToList() },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dlg.FileName, json);
        StatusText = "Profil dışa aktarıldı";
    }

    [RelayCommand]
    private void ImportEscrowProfile()
    {
        var dlg = new OpenFileDialog { Filter = "JSON|*.json" };
        if (dlg.ShowDialog() != true)
            return;

        var paths = _profileService.Import(dlg.FileName);
        ApplyEscrowSelection(paths.ToHashSet(StringComparer.OrdinalIgnoreCase), _autoSuggestedPaths);
        StatusText = $"{paths.Count} dosya profilden yüklendi";
    }

    [RelayCommand]
    private void OpenRecentResource(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var match = Resources.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            SelectResource(match);
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        var config = _configService.Load();
        config.Language = config.Language.Equals("en", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
        _configService.Save(config);
        LocSvc.Instance.SetLanguage(config.Language);
        StatusText = config.Language == "en" ? "Language: English" : "Dil: Türkçe";
    }

    [RelayCommand]
    private void SecurityScan()
    {
        if (_selectedResource == null)
            return;

        var config = _configService.Load();
        var allFiles = Directory
            .EnumerateFiles(_selectedResource.Path, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_selectedResource.Path, f).Replace('\\', '/'))
            .Where(f => !ExcludeHelper.ShouldExclude(f, config.Exclude)
                     && !ExcludeHelper.ShouldExclude(f, config.EscrowZipExtraExcludes))
            .ToList();

        var warnings = SecurityScanService.ScanResource(_selectedResource.Path, allFiles, config.PortalMaxZipMb);

        var text = warnings.Count == 0
            ? "Güvenlik taraması temiz ✓\n\nHassas dosya adı, olası gizli anahtar veya boyut sorunu bulunamadı."
            : "Bulgular:\n" + string.Join(Environment.NewLine, warnings.Select(w => "- " + w));

        TextEditorDialog.ShowPreview(LocSvc.Get("security_scan"), text);
        ShowToast(
            warnings.Count == 0 ? "Güvenlik taraması temiz" : $"{warnings.Count} güvenlik bulgusu",
            warnings.Count == 0 ? ToastKind.Success : ToastKind.Warning);
    }

    [RelayCommand]
    private void Preflight()
    {
        if (_selectedResource == null)
            return;

        var config = _configService.Load();
        var paths = GetSelectedEscrowPaths();
        var summary = ReleasePreviewService.Build(_selectedResource.Path, paths, config);
        var lint = ManifestLintService.Lint(_manifestContent, ManifestFileName);
        var analysis = ManifestAnalyzerService.Analyze(_selectedResource.Path, _manifestContent, config);
        var analysisWarnings = BuildWarningsText(analysis)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        static string Mark(bool ok) => ok ? "✓" : "✗";

        var lines = new List<string>
        {
            $"Ön kontrol — {_selectedResource.Name} v{CurrentVersion}",
            new string('─', 40),
            $"{Mark(summary.EscrowedFiles > 0)} Escrow'da kalacak dosya: {summary.EscrowedFiles}",
            $"{Mark(true)} Açık (escrow_ignore) dosya: {summary.OpenFiles}",
            $"   Tahmini escrow: {summary.EstimatedEscrowBytes / (1024.0 * 1024.0):F2} MB · OS: {summary.EstimatedOsBytes / (1024.0 * 1024.0):F2} MB",
            $"{Mark(lint.Count == 0)} Manifest lint: {(lint.Count == 0 ? "sorun yok" : lint.Count + " sorun")}",
            $"{Mark(analysisWarnings.Length == 0)} Manifest analizi: {(analysisWarnings.Length == 0 ? "temiz" : analysisWarnings.Length + " uyarı")}",
            $"{Mark(summary.Warnings.Count == 0)} Güvenlik/boyut: {(summary.Warnings.Count == 0 ? "temiz" : summary.Warnings.Count + " bulgu")}",
            ""
        };

        void AddSection(string title, IEnumerable<string> items)
        {
            var list = items.ToList();
            if (list.Count == 0)
                return;
            lines.Add(title);
            lines.AddRange(list.Select(i => "  - " + i));
            lines.Add("");
        }

        AddSection("Lint:", lint);
        AddSection("Analiz:", analysisWarnings);
        AddSection("Güvenlik/boyut:", summary.Warnings);

        var clean = lint.Count == 0 && analysisWarnings.Length == 0 && summary.Warnings.Count == 0;
        lines.Add(clean ? "Sonuç: Release'e hazır ✓" : "Sonuç: Yukarıdaki uyarıları gözden geçirin.");

        TextEditorDialog.ShowPreview(LocSvc.Get("preflight"), string.Join(Environment.NewLine, lines));
        ShowToast(clean ? "Ön kontrol temiz" : "Ön kontrolde uyarılar var",
            clean ? ToastKind.Success : ToastKind.Warning);
    }

    [RelayCommand]
    private void CompareHistoryEntry(ReleaseHistoryEntry? entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.EscrowZipPath))
            return;

        var prev = _orchestrator.FindPreviousEscrowZip(entry.ResourceName, entry.EscrowZipPath);
        if (prev == null || !File.Exists(entry.EscrowZipPath))
        {
            ShowToast("Karşılaştırılacak önceki release yok", ToastKind.Info);
            return;
        }

        var diff = ReleaseDiffService.CompareWithPrevious(prev, entry.EscrowZipPath);
        TextEditorDialog.ShowPreview($"{entry.ResourceName} v{entry.Version} — {LocSvc.Get("compare_release")}", diff);
    }

    [RelayCommand]
    private void CopyHistoryPath(ReleaseHistoryEntry? entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.EscrowZipPath))
            return;

        try
        {
            Clipboard.SetText(entry.EscrowZipPath);
            ShowToast("ZIP yolu kopyalandı", ToastKind.Success);
        }
        catch
        {
            ShowToast("Panoya kopyalanamadı", ToastKind.Error);
        }
    }
}
