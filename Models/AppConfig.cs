namespace ReleaseTool.Desktop.Models;

public sealed class AppConfig
{
    public string ResourcesFolder { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public string EscrowWebPublishFolder { get; set; } = "dist";
    public List<string> EscrowZipExtraExcludes { get; set; } = [];
    public List<string> Exclude { get; set; } = [];
    public string? LastResource { get; set; }

    /// <summary>Ek escrow otomatik klasör adları (ör. "modules", "data").</summary>
    public List<string> AutoEscrowDirs { get; set; } = [];

    /// <summary>Bu klasörlerdeki tüm dosyalar otomatik seçilir (varsayılan: editable).</summary>
    public List<string> AutoEscrowAllExtensionsDirs { get; set; } = [];

    /// <summary>Ek dosya adları — tam eşleşme (ör. "database.json").</summary>
    public List<string> AutoEscrowFileNames { get; set; } = [];

    /// <summary>Ek .lua dosya kök adları — uzantısız (ör. "bridge").</summary>
    public List<string> AutoEscrowFileStems { get; set; } = [];

    public bool DarkTheme { get; set; }

    /// <summary>DPAPI ile şifrelenmiş forum.cfx.re _t cookie.</summary>
    public string ForumCookieProtected { get; set; } = "";

    /// <summary>WebView2 girişinden alınan portal oturum cookie'leri (JSON, DPAPI).</summary>
    public string PortalSessionProtected { get; set; } = "";

    public List<PortalAssetMapping> PortalAssets { get; set; } = [];

    // Pipeline & integrations
    public bool AutoUploadPortal { get; set; }
    public bool RunWebBuildBeforeRelease { get; set; } = true;
    public string WebBuildCommand { get; set; } = "npm run build";
    public bool VerifyZipAfterRelease { get; set; } = true;
    public bool GitTagOnRelease { get; set; }
    public bool GitCommitOnRelease { get; set; }
    public bool UseGitChangelog { get; set; }
    public string DiscordWebhookProtected { get; set; } = "";
    public string GitHubTokenProtected { get; set; } = "";
    public string GitHubRepo { get; set; } = "";
    public bool UploadOsZipToGitHub { get; set; } = true;
    public string TebexPrivateKeyProtected { get; set; } = "";
    public string TebexPackageId { get; set; } = "";
    public long PortalMaxZipMb { get; set; } = 500;
    public bool WatchResourcesFolder { get; set; }
    public string Language { get; set; } = "tr";
    public List<string> ResourcesFolders { get; set; } = [];
    public List<string> RecentResources { get; set; } = [];
    public bool CheckForUpdates { get; set; } = true;
    public string EscrowProfileName { get; set; } = "";

    // UI / kişiselleştirme
    public string AccentColor { get; set; } = "";
    public bool FollowSystemTheme { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double LeftPanelWidth { get; set; } = 280;

    /// <summary>İsimli escrow seçim preset'leri (resource'tan bağımsız).</summary>
    public List<EscrowPreset> EscrowPresets { get; set; } = [];
}
