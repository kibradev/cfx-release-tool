using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReleaseTool.Desktop.Models;
using ReleaseTool.Desktop.Services;
using ReleaseTool.Desktop.Views;
using LocSvc = ReleaseTool.Desktop.Services.Loc;

namespace ReleaseTool.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService = new();
    private readonly ReleaseService _releaseService;
    private readonly ReleaseOrchestrator _orchestrator = new();
    private readonly EscrowProfileService _profileService = new();
    private readonly SelectionStore _selectionStore = new();
    private readonly ReleaseHistoryService _historyService = new();
    private readonly PortalUploadService _portalService = new();
    private ResourceInfo? _selectedResource;
    private bool _suppressTreeUpdates;
    private string _manifestContent = "";
    private HashSet<string> _autoSuggestedPaths = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _releaseCts;

    public ObservableCollection<VersionBumpOption> VersionBumpOptions { get; } =
    [
        new() { Kind = VersionBumpKind.Patch, Label = "Patch +1 (1.0.0 → 1.0.1)" },
        new() { Kind = VersionBumpKind.Minor, Label = "Minor +1 (1.0.0 → 1.1.0)" },
        new() { Kind = VersionBumpKind.Major, Label = "Major +1 (1.0.0 → 2.0.0)" },
        new() { Kind = VersionBumpKind.None, Label = "Sürüm artırma" },
        new() { Kind = VersionBumpKind.Manual, Label = "Manuel sürüm" }
    ];

    public LocSvc Loc => LocSvc.Instance;

    public MainViewModel()
    {
        _releaseService = new ReleaseService(_configService);
        SelectedVersionBump = VersionBumpOptions[0];
        LoadConfig();
        RefreshHistory();
        RefreshPresets();
        _ = CheckUpdatesOnStartupAsync();
    }

    [ObservableProperty]
    private string _resourcesFolder = "";

    [ObservableProperty]
    private string _outputFolder = "";

    [ObservableProperty]
    private ObservableCollection<ResourceInfo> _resources = [];

    [ObservableProperty]
    private ObservableCollection<FileTreeNode> _fileTree = [];

    [ObservableProperty]
    private ObservableCollection<ReleaseZipResult> _lastZips = [];

    [ObservableProperty]
    private ObservableCollection<ReleaseHistoryEntry> _releaseHistory = [];

    [ObservableProperty]
    private string? _selectedResourceName;

    [ObservableProperty]
    private string _manifestFileName = "";

    [ObservableProperty]
    private string _currentVersion = "—";

    [ObservableProperty]
    private bool _bumpOnRelease = true;

    [ObservableProperty]
    private VersionBumpOption? _selectedVersionBump;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private int _selectedEscrowCount;

    [ObservableProperty]
    private string _fileTreeFilter = "";

    [ObservableProperty]
    private string _manifestWarnings = "";

    [ObservableProperty]
    private bool _darkTheme;

    [ObservableProperty]
    private int _batchSelectedCount;

    [ObservableProperty]
    private string _manualVersionText = "";

    [ObservableProperty]
    private int _progressPercent = -1;

    [ObservableProperty]
    private bool _watchEnabled;

    [ObservableProperty]
    private ObservableCollection<string> _recentResourceNames = [];

    [ObservableProperty]
    private ObservableCollection<string> _resourcesFolderList = [];

    [ObservableProperty]
    private string? _selectedResourcesFolder;

    [ObservableProperty]
    private ObservableCollection<ToastItem> _toasts = [];

    [ObservableProperty]
    private bool _isReleasing;

    [ObservableProperty]
    private ObservableCollection<string> _presetNames = [];

    [ObservableProperty]
    private string? _selectedPresetName;

    public bool ShowManualVersion => SelectedVersionBump?.Kind == VersionBumpKind.Manual;
    public bool ShowProgress => ProgressPercent >= 0;

    public bool HasSelectedResource => _selectedResource != null;
    public bool HasLastZips => LastZips.Count > 0;
    public bool HasHistory => ReleaseHistory.Count > 0;
    public bool HasManifestWarnings => !string.IsNullOrWhiteSpace(ManifestWarnings);

    partial void OnLastZipsChanged(ObservableCollection<ReleaseZipResult> value)
    {
        OnPropertyChanged(nameof(HasLastZips));
        UploadToPortalCommand.NotifyCanExecuteChanged();
    }
    partial void OnReleaseHistoryChanged(ObservableCollection<ReleaseHistoryEntry> value) => OnPropertyChanged(nameof(HasHistory));
    partial void OnSelectedResourceNameChanged(string? value) => OnPropertyChanged(nameof(HasSelectedResource));
    partial void OnManifestWarningsChanged(string value) => OnPropertyChanged(nameof(HasManifestWarnings));

    partial void OnFileTreeFilterChanged(string value)
    {
        FileTreeFilterService.ApplyFilter(FileTree, value);
    }

    partial void OnDarkThemeChanged(bool value)
    {
        var config = _configService.Load();
        ThemeService.Apply(value, config.AccentColor);
        config.DarkTheme = value;
        _configService.Save(config);
    }

    partial void OnBumpOnReleaseChanged(bool value)
    {
        if (value && SelectedVersionBump?.Kind == VersionBumpKind.None)
            SelectedVersionBump = VersionBumpOptions[0];
        if (!value)
            SelectedVersionBump = VersionBumpOptions.First(o => o.Kind == VersionBumpKind.None);
    }

    partial void OnSelectedVersionBumpChanged(VersionBumpOption? value) =>
        OnPropertyChanged(nameof(ShowManualVersion));

    private void LoadConfig()
    {
        var config = _configService.Load();
        ResourcesFolder = config.ResourcesFolder;
        OutputFolder = _configService.GetOutputFolder(config);
        DarkTheme = config.FollowSystemTheme ? ThemeService.IsSystemDark() : config.DarkTheme;
        WatchEnabled = config.WatchResourcesFolder;
        ThemeService.Apply(DarkTheme, config.AccentColor);
        LocSvc.Instance.SetLanguage(config.Language);
        RefreshPresets();

        ResourcesFolderList = new ObservableCollection<string>(
            new[] { config.ResourcesFolder }
                .Concat(config.ResourcesFolders)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        SelectedResourcesFolder = ResourcesFolder;
        RecentResourceNames = new ObservableCollection<string>(config.RecentResources.Take(8));

        if (WatchEnabled && !string.IsNullOrWhiteSpace(ResourcesFolder))
            AppServices.FolderWatch.Start(ResourcesFolder, () =>
                Application.Current.Dispatcher.Invoke(RefreshResourcesList));

        if (!string.IsNullOrWhiteSpace(config.ResourcesFolder) && Directory.Exists(config.ResourcesFolder))
            RefreshResourcesList(config.LastResource);
    }

    private void SaveConfig(string? lastResource = null)
    {
        var config = _configService.Load();
        config.ResourcesFolder = ResourcesFolder;
        config.OutputFolder = OutputFolder;
        config.DarkTheme = DarkTheme;
        config.WatchResourcesFolder = WatchEnabled;
        config.ResourcesFolders = ResourcesFolderList
            .Where(f => !string.Equals(f, ResourcesFolder, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (lastResource != null)
        {
            config.LastResource = lastResource;
            var recent = config.RecentResources.Where(r => !string.Equals(r, lastResource, StringComparison.OrdinalIgnoreCase)).ToList();
            recent.Insert(0, lastResource);
            config.RecentResources = recent.Take(12).ToList();
            RecentResourceNames = new ObservableCollection<string>(config.RecentResources.Take(8));
        }
        _configService.Save(config);
    }

    private void RefreshHistory()
    {
        ReleaseHistory = new ObservableCollection<ReleaseHistoryEntry>(_historyService.Load());
    }

    public void ShowToast(string message, ToastKind kind = ToastKind.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var toast = new ToastItem { Message = message, Kind = kind };
        Toasts.Add(toast);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(kind == ToastKind.Error ? 6 : 3.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Toasts.Remove(toast);
        };
        timer.Start();
    }

    [RelayCommand]
    private void DismissToast(ToastItem? toast)
    {
        if (toast != null)
            Toasts.Remove(toast);
    }

    private void RefreshPresets()
    {
        var config = _configService.Load();
        PresetNames = new ObservableCollection<string>(config.EscrowPresets.Select(p => p.Name));
    }

    [RelayCommand]
    private void SavePreset()
    {
        if (_selectedResource == null)
            return;

        var name = _selectedResource.Name;
        if (!PromptText("Preset adı", ref name) || string.IsNullOrWhiteSpace(name))
            return;

        var config = _configService.Load();
        var paths = GetSelectedEscrowPaths().ToList();
        var existing = config.EscrowPresets.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            existing.Paths = paths;
        else
            config.EscrowPresets.Add(new EscrowPreset { Name = name.Trim(), Paths = paths });

        _configService.Save(config);
        RefreshPresets();
        SelectedPresetName = name.Trim();
        ShowToast($"Preset kaydedildi: {name} ({paths.Count} dosya)", ToastKind.Success);
    }

    partial void OnSelectedPresetNameChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || _selectedResource == null)
            return;

        var config = _configService.Load();
        var preset = config.EscrowPresets.FirstOrDefault(p =>
            string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
        if (preset == null)
            return;

        ApplyEscrowSelection(preset.Paths.ToHashSet(StringComparer.OrdinalIgnoreCase), _autoSuggestedPaths);
        PersistCurrentSelection();
        ShowToast($"Preset uygulandı: {value}", ToastKind.Info);
    }

    private static bool PromptText(string title, ref string value)
    {
        var input = value;
        var result = TextEditorDialog.ShowEdit(title, ref input);
        if (result != true)
            return false;
        value = input.Trim();
        return true;
    }

    public void SetResourcesFolderFromDrop(string path)
    {
        if (!Directory.Exists(path))
            return;

        ResourcesFolder = path;
        SaveConfig();
        RefreshResourcesList();
    }

    /// <summary>
    /// Bırakılan klasör tek bir resource ise (fxmanifest içeriyorsa) üst klasörü
    /// resources kökü yap ve o resource'u seç; değilse kökü ayarla.
    /// </summary>
    public void SetDroppedPath(string path)
    {
        if (!Directory.Exists(path))
            return;

        if (ManifestService.FindManifestPath(path) != null)
        {
            var parent = Directory.GetParent(path)?.FullName;
            var name = new DirectoryInfo(path).Name;
            if (!string.IsNullOrEmpty(parent))
            {
                ResourcesFolder = parent;
                SaveConfig();
                RefreshResourcesList(name);
                ShowToast($"Resource açıldı: {name}", ToastKind.Success);
                return;
            }
        }

        SetResourcesFolderFromDrop(path);
        ShowToast("Resources klasörü ayarlandı", ToastKind.Info);
    }

    [RelayCommand]
    private void BrowseResourcesFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Resources klasörünü seçin" };
        if (!string.IsNullOrWhiteSpace(ResourcesFolder) && Directory.Exists(ResourcesFolder))
            dialog.InitialDirectory = ResourcesFolder;

        if (dialog.ShowDialog() != true)
            return;

        ResourcesFolder = dialog.FolderName;
        SaveConfig();
        RefreshResourcesList();
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Çıktı klasörünü seçin" };
        if (!string.IsNullOrWhiteSpace(OutputFolder) && Directory.Exists(OutputFolder))
            dialog.InitialDirectory = OutputFolder;

        if (dialog.ShowDialog() != true)
            return;

        OutputFolder = dialog.FolderName;
        SaveConfig();
    }

    [RelayCommand]
    private void RefreshResources() => RefreshResourcesList();

    [RelayCommand]
    private void ToggleDarkTheme() => DarkTheme = !DarkTheme;

    [RelayCommand]
    private void OpenPortalSettings()
    {
        var assetId = GetPortalAssetId(_selectedResource?.Name);
        var dlg = new PortalSettingsDialog(_selectedResource?.Name, assetId)
        {
            Owner = Application.Current.MainWindow
        };
        dlg.ShowDialog();
    }

    [RelayCommand(CanExecute = nameof(CanUploadToPortal))]
    private async Task UploadToPortalAsync()
    {
        var escrow = LastZips.FirstOrDefault(z => z.Mode == "escrow");
        if (escrow == null || _selectedResource == null)
            return;

        var cookie = GetForumCookie();
        if (string.IsNullOrEmpty(cookie) && !_portalService.HasPortalSession())
        {
            MessageBox.Show(
                "Portal oturumu yok.\n\nPortal ayarları → 'Tarayıcıdan giriş yap' kullanın.",
                "Portal",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            OpenPortalSettings();
            return;
        }

        var assetId = GetPortalAssetId(_selectedResource.Name);
        if (assetId <= 0)
        {
            var search = await _portalService.SearchAssetsAsync(cookie, _selectedResource.Name);
            assetId = AssetMatcherService.FindBestAssetId(_selectedResource.Name, search);
        }

        if (assetId <= 0)
        {
            MessageBox.Show(
                "Bu resource için Portal Asset ID bulunamadı.\n\nPortal ayarlarından ID girin veya ara.",
                "Portal",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            OpenPortalSettings();
            return;
        }

        IsBusy = true;
        try
        {
            var manifestFile = ManifestService.FindManifestPath(_selectedResource.Path)!;
            var manifest = ManifestService.ReadManifest(_selectedResource.Path, manifestFile);
            var version = ManifestService.GetCurrentVersion(manifest)?.Full ?? CurrentVersion;
            var changelog = PortalUploadService.ReadChangelogFromResource(_selectedResource.Path);
            var rc = PortalUploadService.IsReleaseCandidate(manifest);

            var progress = new Progress<string>(msg => StatusText = msg);
            await _portalService.UploadEscrowZipAsync(
                string.IsNullOrEmpty(cookie) ? null : cookie,
                assetId, escrow.ZipPath, version, changelog, rc, progress);

            MessageBox.Show(
                $"Escrow ZIP Portal'a yüklendi.\n\nAsset ID: {assetId}\nv{version}",
                "Portal upload",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Process.Start(new ProcessStartInfo
            {
                FileName = PortalUploadService.GetPortalAssetUrl(assetId),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message + "\n\nCookie süresi dolmuş olabilir. Portal ayarlarından yenileyin.",
                "Portal upload başarısız",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUploadToPortal() => !IsBusy && HasLastZips && _selectedResource != null;

    private string GetForumCookie()
    {
        var config = _configService.Load();
        return SecretProtector.Unprotect(config.ForumCookieProtected);
    }

    private int GetPortalAssetId(string? resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
            return 0;

        var config = _configService.Load();
        return config.PortalAssets
            .FirstOrDefault(p => string.Equals(p.ResourceName, resourceName, StringComparison.OrdinalIgnoreCase))
            ?.AssetId ?? 0;
    }

    private async Task OfferPortalUploadAsync()
    {
        var escrow = LastZips.FirstOrDefault(z => z.Mode == "escrow");
        var config = _configService.Load();
        if (escrow == null)
            return;

        if (config.AutoUploadPortal)
        {
            await UploadToPortalAsync();
            return;
        }

        if (string.IsNullOrEmpty(GetForumCookie()) && !_portalService.HasPortalSession())
            return;

        var result = MessageBox.Show(
            "Escrow ZIP Portal'a yüklensin mi?",
            "Portal upload",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            await UploadToPortalAsync();
    }

    [RelayCommand]
    private void OpenKeymasterPortal()
    {
        if (_selectedResource != null)
        {
            var assetId = GetPortalAssetId(_selectedResource.Name);
            if (assetId > 0)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = PortalUploadService.GetPortalAssetUrl(assetId),
                    UseShellExecute = true
                });
                return;
            }
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "https://portal.cfx.re/assets/created-assets",
            UseShellExecute = true
        });
    }

    private void RefreshResourcesList(string? selectName = null)
    {
        Resources.Clear();
        if (string.IsNullOrWhiteSpace(ResourcesFolder) || !Directory.Exists(ResourcesFolder))
            return;

        foreach (var item in ResourceScanner.ScanResources(ResourcesFolder))
            Resources.Add(item);

        UpdateBatchCount();

        var target = selectName ?? SelectedResourceName;
        if (string.IsNullOrWhiteSpace(target))
            return;

        var match = Resources.FirstOrDefault(r =>
            string.Equals(r.Name, target, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            return;

        if (_selectedResource == null ||
            !string.Equals(_selectedResource.Path, match.Path, StringComparison.OrdinalIgnoreCase))
        {
            SelectResource(match);
            return;
        }

        _selectedResource = match;
        SelectedResourceName = match.Name;
        CurrentVersion = match.Version ?? CurrentVersion;
    }

    [RelayCommand]
    private void SelectResource(ResourceInfo? resource)
    {
        if (resource == null)
            return;

        PersistCurrentSelection();
        _selectedResource = resource;
        SelectedResourceName = resource.Name;
        SaveConfig(resource.Name);
        _ = LoadResourceDetailsAsync(resource);
    }

    private async Task LoadResourceDetailsAsync(ResourceInfo resource)
    {
        IsBusy = true;
        StatusText = "Dosyalar okunuyor…";
        LastZips.Clear();

        try
        {
            await Task.Run(() =>
            {
                var manifestFile = ManifestService.FindManifestPath(resource.Path)
                    ?? throw new InvalidOperationException("fxmanifest.lua bulunamadı");

                var (content, versionParts) = ManifestService.ReadManifestEnsuringVersion(resource.Path, manifestFile);
                var version = versionParts.Full;
                var escrowIgnore = ManifestService.ParseEscrowIgnorePaths(content);
                var tree = FileTreeService.BuildTree(resource.Path);
                var filePaths = FileTreeService.CollectAllFilePaths(tree);
                var appConfig = _configService.Load();
                var autoSuggested = EscrowIgnoreService.GetAutoSuggestedPaths(filePaths, appConfig);

                var saved = _selectionStore.Load(resource.Path);
                HashSet<string> selection;
                if (saved is { Count: > 0 })
                {
                    selection = saved.ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    selection = EscrowIgnoreService.BuildInitialEscrowSelection(escrowIgnore, filePaths, appConfig);
                }

                var analysis = ManifestAnalyzerService.Analyze(resource.Path, content, appConfig);
                var warnings = BuildWarningsText(analysis);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _manifestContent = content;
                    _autoSuggestedPaths = autoSuggested;
                    ManifestFileName = manifestFile;
                    CurrentVersion = version;
                    ManifestWarnings = warnings;
                    FileTree = new ObservableCollection<FileTreeNode>(tree);
                    ApplyEscrowSelection(selection, autoSuggested);
                    FileTreeFilterService.ApplyFilter(FileTree, FileTreeFilter);
                });
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            FileTree.Clear();
            CurrentVersion = "—";
            ManifestWarnings = "";
        }
        finally
        {
            IsBusy = false;
            StatusText = "";
        }
    }

    private static string BuildWarningsText(ManifestAnalysisResult analysis)
    {
        var lines = new List<string>();
        if (analysis.MissingFiles.Count > 0)
            lines.Add($"Eksik dosya ({analysis.MissingFiles.Count}): " + string.Join(", ", analysis.MissingFiles.Take(8)) + (analysis.MissingFiles.Count > 8 ? "…" : ""));
        if (!string.IsNullOrWhiteSpace(analysis.WebBuildWarning))
            lines.Add(analysis.WebBuildWarning);
        return string.Join("\n", lines);
    }

    private void PersistCurrentSelection()
    {
        if (_selectedResource == null || FileTree.Count == 0)
            return;

        _selectionStore.Save(_selectedResource.Path, GetSelectedEscrowPaths());
    }

    private void ApplyEscrowSelection(HashSet<string> selected, HashSet<string>? autoSuggested = null)
    {
        _suppressTreeUpdates = true;
        try
        {
            foreach (var node in FileTree)
                ApplySelectionToNode(node, selected, autoSuggested);
            UpdateSelectedCount();
        }
        finally
        {
            _suppressTreeUpdates = false;
        }
    }

    private static void ApplySelectionToNode(FileTreeNode node, HashSet<string> selected, HashSet<string>? autoSuggested)
    {
        if (!node.IsDirectory)
        {
            node.IsChecked = selected.Contains(node.Path);
            node.IsAutoSuggested = autoSuggested?.Contains(node.Path) == true;
            return;
        }

        foreach (var child in node.Children)
            ApplySelectionToNode(child, selected, autoSuggested);

        UpdateFolderCheckState(node);
    }

    private static void UpdateFolderCheckState(FileTreeNode folder)
    {
        var files = folder.DescendantFiles().Where(f => !f.IsDirectory).ToList();
        if (files.Count == 0)
        {
            folder.IsChecked = false;
            return;
        }

        var checkedCount = files.Count(f => f.IsChecked == true);
        folder.IsChecked = checkedCount switch
        {
            0 => false,
            _ when checkedCount == files.Count => true,
            _ => null
        };
    }

    public void OnFileCheckChanged(FileTreeNode node, bool? isChecked)
    {
        if (_suppressTreeUpdates)
            return;

        _suppressTreeUpdates = true;
        try
        {
            if (node.IsDirectory)
            {
                foreach (var file in node.DescendantFiles().Where(f => !f.IsDirectory))
                    file.IsChecked = isChecked == true;
                node.IsChecked = isChecked;
            }
            else
            {
                node.IsChecked = isChecked;
            }

            var current = node.IsDirectory ? node : node.Parent;
            while (current != null)
            {
                UpdateFolderCheckState(current);
                current = current.Parent;
            }

            UpdateSelectedCount();
            PersistCurrentSelection();
        }
        finally
        {
            _suppressTreeUpdates = false;
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedEscrowCount = FileTree
            .SelectMany(n => n.DescendantFiles())
            .Count(f => !f.IsDirectory && f.IsChecked == true);
    }

    private HashSet<string> GetSelectedEscrowPaths()
    {
        return FileTree
            .SelectMany(n => n.DescendantFiles())
            .Where(f => !f.IsDirectory && f.IsChecked == true)
            .Select(f => f.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<FileTreeNode> AllFileNodes() =>
        FileTree.SelectMany(n => n.DescendantFiles()).Where(f => !f.IsDirectory);

    [RelayCommand]
    private void SelectAllLua() => SetSelection(f => f.Path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase), true);

    [RelayCommand]
    private void SelectAllFiles() => SetSelection(_ => true, true);

    [RelayCommand]
    private void ClearSelection() => SetSelection(_ => true, false);

    [RelayCommand]
    private void KeepOnlyAutoSuggested() => SetSelection(f => _autoSuggestedPaths.Contains(f.Path), true, clearOthers: true);

    private void SetSelection(Func<FileTreeNode, bool> predicate, bool check, bool clearOthers = false)
    {
        _suppressTreeUpdates = true;
        try
        {
            foreach (var file in AllFileNodes())
            {
                if (clearOthers)
                    file.IsChecked = predicate(file) && check;
                else if (predicate(file))
                    file.IsChecked = check;
            }

            foreach (var root in FileTree)
                UpdateFolderStatesRecursive(root);

            UpdateSelectedCount();
            PersistCurrentSelection();
        }
        finally
        {
            _suppressTreeUpdates = false;
        }
    }

    private static void UpdateFolderStatesRecursive(FileTreeNode node)
    {
        if (node.IsDirectory)
        {
            foreach (var child in node.Children)
                UpdateFolderStatesRecursive(child);
            UpdateFolderCheckState(node);
        }
    }

    [RelayCommand]
    private void PreviewManifest()
    {
        if (_selectedResource == null)
            return;

        var paths = GetSelectedEscrowPaths();
        var preview = ManifestService.ExtractEscrowIgnoreBlock(_manifestContent, paths);
        TextEditorDialog.ShowPreview("Escrow manifest önizleme", preview);
    }

    [RelayCommand]
    private void EditReleaseIgnore()
    {
        if (_selectedResource == null)
            return;

        var path = Path.Combine(_selectedResource.Path, "release.ignore");
        var content = File.Exists(path) ? File.ReadAllText(path) : "# release.ignore\n# Her satır bir exclude kuralı\n";
        if (TextEditorDialog.ShowEdit("release.ignore düzenle", ref content) != true)
            return;

        File.WriteAllText(path, content);
        StatusText = "release.ignore kaydedildi";
    }

    [RelayCommand]
    private void ToggleBatchResource(ResourceInfo? resource)
    {
        if (resource == null)
            return;

        resource.IsBatchSelected = !resource.IsBatchSelected;
        UpdateBatchCount();
    }

    [RelayCommand]
    private void SelectAllBatch()
    {
        foreach (var r in Resources)
            r.IsBatchSelected = true;
        UpdateBatchCount();
    }

    [RelayCommand]
    private void ClearBatch()
    {
        foreach (var r in Resources)
            r.IsBatchSelected = false;
        UpdateBatchCount();
    }

    private void UpdateBatchCount() => BatchSelectedCount = Resources.Count(r => r.IsBatchSelected);

    private VersionBumpKind GetBumpKind() =>
        BumpOnRelease ? (SelectedVersionBump?.Kind ?? VersionBumpKind.Patch) : VersionBumpKind.None;

    private string? GetManualVersion()
    {
        if (GetBumpKind() != VersionBumpKind.Manual)
            return null;
        var v = ManualVersionText.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private ReleasePipelineOptions BuildPipelineOptions(bool autoPortal = false) => new()
    {
        ManualVersion = GetManualVersion(),
        AutoUploadPortal = autoPortal
    };

    private bool ConfirmReleaseWarnings()
    {
        if (string.IsNullOrWhiteSpace(ManifestWarnings))
            return true;

        var result = MessageBox.Show(
            ManifestWarnings + "\n\nYine de release oluşturmak istiyor musunuz?",
            "Uyarılar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    [RelayCommand(CanExecute = nameof(CanRunRelease))]
    private async Task CreateReleaseAsync()
    {
        if (_selectedResource == null)
            return;

        if (!ConfirmReleaseWarnings())
            return;

        _releaseCts = new CancellationTokenSource();
        var ct = _releaseCts.Token;

        IsBusy = true;
        IsReleasing = true;
        ProgressPercent = 0;
        StatusText = "Release oluşturuluyor…";

        try
        {
            PersistCurrentSelection();
            var escrowPaths = GetSelectedEscrowPaths();

            if (GetBumpKind() == VersionBumpKind.Manual && string.IsNullOrWhiteSpace(ManualVersionText))
            {
                MessageBox.Show("Manuel sürüm girin.", "Release", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var bump = GetBumpKind() == VersionBumpKind.Manual ? VersionBumpKind.None : GetBumpKind();
            var progress = new Progress<(string Message, int Percent)>(p =>
            {
                StatusText = p.Message;
                if (p.Percent >= 0)
                    ProgressPercent = p.Percent;
            });

            var (version, releases) = await _orchestrator.RunReleaseAsync(
                _selectedResource.Path,
                _selectedResource.Name,
                escrowPaths,
                bump,
                BuildPipelineOptions(),
                progress,
                ct);

            FinishRelease(_selectedResource, version, releases);
            RefreshResourcesList(_selectedResource.Name);
            ShowToast($"{_selectedResource?.Name} v{version} hazır", ToastKind.Success);

            if (!_configService.Load().AutoUploadPortal)
                await OfferPortalUploadAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Release iptal edildi";
            ShowToast("Release iptal edildi", ToastKind.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Release oluşturulamadı", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "";
            ShowToast("Release başarısız", ToastKind.Error);
        }
        finally
        {
            IsBusy = false;
            IsReleasing = false;
            ProgressPercent = -1;
            _releaseCts?.Dispose();
            _releaseCts = null;
        }
    }

    [RelayCommand]
    private void CancelRelease()
    {
        _releaseCts?.Cancel();
        StatusText = "İptal ediliyor…";
    }

    [RelayCommand(CanExecute = nameof(CanRunBatchRelease))]
    private async Task CreateBatchReleaseAsync()
    {
        var batch = Resources.Where(r => r.IsBatchSelected).ToList();
        if (batch.Count == 0)
            return;

        var confirm = MessageBox.Show(
            $"{batch.Count} resource için release oluşturulacak. Devam?",
            "Toplu release",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        var ok = 0;
        var fail = 0;

        try
        {
            foreach (var resource in batch)
            {
                StatusText = $"Release: {resource.Name}…";
                try
                {
                    var escrowPaths = ResolveEscrowPathsForResource(resource);
                    var bump = GetBumpKind() == VersionBumpKind.Manual ? VersionBumpKind.None : GetBumpKind();
                    var (version, releases) = await _orchestrator.RunReleaseAsync(
                        resource.Path,
                        resource.Name,
                        escrowPaths,
                        bump,
                        BuildPipelineOptions(autoPortal: _configService.Load().AutoUploadPortal));

                    FinishRelease(resource, version, releases, openFolder: false);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    MessageBox.Show($"{resource.Name}: {ex.Message}", "Toplu release hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            StatusText = $"Toplu release tamamlandı — {ok} başarılı, {fail} hata";
            RefreshHistory();
            RefreshResourcesList(_selectedResource?.Name);

            if (ok > 0)
            {
                var config = _configService.Load();
                OpenInExplorer(_configService.GetOutputFolder(config));
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private HashSet<string> ResolveEscrowPathsForResource(ResourceInfo resource)
    {
        if (_selectedResource != null &&
            string.Equals(_selectedResource.Path, resource.Path, StringComparison.OrdinalIgnoreCase))
        {
            return GetSelectedEscrowPaths();
        }

        var saved = _selectionStore.Load(resource.Path);
        if (saved is { Count: > 0 })
            return saved.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tree = FileTreeService.BuildTree(resource.Path);
        var filePaths = FileTreeService.CollectAllFilePaths(tree);
        var config = _configService.Load();
        var manifestFile = ManifestService.FindManifestPath(resource.Path);
        var patterns = manifestFile != null
            ? ManifestService.ParseEscrowIgnorePaths(ManifestService.ReadManifest(resource.Path, manifestFile))
            : [];

        return EscrowIgnoreService.BuildInitialEscrowSelection(patterns, filePaths, config);
    }

    private void FinishRelease(ResourceInfo resource, string version, List<ReleaseZipResult> releases, bool openFolder = true)
    {
        LastZips = new ObservableCollection<ReleaseZipResult>(releases);
        if (_selectedResource != null &&
            string.Equals(_selectedResource.Path, resource.Path, StringComparison.OrdinalIgnoreCase))
        {
            CurrentVersion = version;
        }

        var escrow = releases.FirstOrDefault(r => r.Mode == "escrow");
        var os = releases.FirstOrDefault(r => r.Mode == "opensource");
        _historyService.Add(new ReleaseHistoryEntry
        {
            ResourceName = resource.Name,
            ResourcePath = resource.Path,
            Version = version,
            CreatedAt = DateTime.Now,
            EscrowBytes = escrow?.Bytes ?? 0,
            OsBytes = os?.Bytes ?? 0,
            EscrowZipPath = escrow?.ZipPath ?? "",
            OsZipPath = os?.ZipPath ?? ""
        });
        RefreshHistory();

        var totalMb = releases.Sum(r => r.Bytes) / (1024.0 * 1024.0);
        StatusText = $"{resource.Name} — v{version} ({totalMb:F2} MB)";

        if (openFolder && releases.Count > 0)
        {
            var folder = Path.GetDirectoryName(releases[0].ZipPath);
            if (!string.IsNullOrEmpty(folder))
                OpenInExplorer(folder);
        }
    }

    private bool CanRunRelease() => !IsBusy && _selectedResource != null;
    private bool CanRunBatchRelease() => !IsBusy && BatchSelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanRunPatch))]
    private void BumpPatch()
    {
        if (_selectedResource == null)
            return;

        var kind = SelectedVersionBump?.Kind ?? VersionBumpKind.Patch;
        if (kind == VersionBumpKind.None)
            kind = VersionBumpKind.Patch;

        var confirm = MessageBox.Show(
            $"\"{_selectedResource.Name}\" sürümünü {kind} artırmak istiyor musunuz?\n\nZIP oluşturulmayacak.",
            "Sürüm artır",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var next = _releaseService.BumpVersionOnly(_selectedResource.Path, kind);
            CurrentVersion = next;
            StatusText = $"Sürüm güncellendi: v{next}";
            RefreshResourcesList(_selectedResource.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Sürüm artırma başarısız", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanRunPatch() => !IsBusy && _selectedResource != null;

    [RelayCommand]
    private void OpenZip(ReleaseZipResult? zip)
    {
        if (zip == null || !File.Exists(zip.ZipPath))
        {
            MessageBox.Show("ZIP dosyası bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = zip.ZipPath, UseShellExecute = true });
    }

    [RelayCommand]
    private void ShowZipFolder(ReleaseZipResult? zip)
    {
        if (zip == null)
            return;

        var folder = Path.GetDirectoryName(zip.ZipPath);
        if (!string.IsNullOrEmpty(folder))
            OpenInExplorer(folder);
    }

    [RelayCommand]
    private void OpenHistoryEntry(ReleaseHistoryEntry? entry)
    {
        if (entry == null)
            return;

        if (!string.IsNullOrEmpty(entry.EscrowZipPath) && File.Exists(entry.EscrowZipPath))
            OpenInExplorer(Path.GetDirectoryName(entry.EscrowZipPath)!);
    }

    private static void OpenInExplorer(string path)
    {
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    partial void OnIsBusyChanged(bool value)
    {
        CreateReleaseCommand.NotifyCanExecuteChanged();
        CreateBatchReleaseCommand.NotifyCanExecuteChanged();
        BumpPatchCommand.NotifyCanExecuteChanged();
        UploadToPortalCommand.NotifyCanExecuteChanged();
    }

    partial void OnBatchSelectedCountChanged(int value) =>
        CreateBatchReleaseCommand.NotifyCanExecuteChanged();
}
