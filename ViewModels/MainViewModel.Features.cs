using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReleaseTool.Desktop.Models;
using ReleaseTool.Desktop.Services;
using ReleaseTool.Desktop.Views;

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
        TextEditorDialog.ShowPreview(Loc.Get("preview_escrow"), ReleasePreviewService.FormatSummary(summary));
    }

    [RelayCommand]
    private void EditFxManifest()
    {
        if (_selectedResource == null || string.IsNullOrEmpty(ManifestFileName))
            return;

        var path = Path.Combine(_selectedResource.Path, ManifestFileName);
        var content = File.ReadAllText(path);
        if (TextEditorDialog.ShowEdit(Loc.Get("edit_manifest"), ref content) != true)
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

        if (TextEditorDialog.ShowEdit(Loc.Get("edit_changelog"), ref content) != true)
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
        TextEditorDialog.ShowPreview(Loc.Get("verify_zip"), string.Join(Environment.NewLine, lines));
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
        TextEditorDialog.ShowPreview(Loc.Get("compare_release"), diff);
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
        StatusText = config.Language == "en" ? "Language: English" : "Dil: Türkçe";
    }
}
