using System.Windows;
using System.Windows.Controls;
using ReleaseTool.Desktop.Models;
using ReleaseTool.Desktop.Services;

namespace ReleaseTool.Desktop.Views;

public partial class AppSettingsDialog : Window
{
    private readonly ConfigService _configService = new();
    private AppConfig _config = new();

    public AppSettingsDialog()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _config = _configService.Load();

        LanguageBox.SelectedIndex = _config.Language.Equals("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        WatchFolderBox.IsChecked = _config.WatchResourcesFolder;
        CheckUpdatesBox.IsChecked = _config.CheckForUpdates;
        AutoPortalBox.IsChecked = _config.AutoUploadPortal;
        VerifyZipBox.IsChecked = _config.VerifyZipAfterRelease;

        FollowSystemThemeBox.IsChecked = _config.FollowSystemTheme;
        AccentBox.Items.Clear();
        AccentBox.Items.Add(new ComboBoxItem { Content = "Varsayılan", Tag = "" });
        foreach (var (name, hex) in ThemeService.AccentPresets)
            AccentBox.Items.Add(new ComboBoxItem { Content = name, Tag = hex });
        AccentBox.SelectedIndex = 0;
        for (var i = 0; i < AccentBox.Items.Count; i++)
        {
            if (AccentBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), _config.AccentColor, StringComparison.OrdinalIgnoreCase))
            {
                AccentBox.SelectedIndex = i;
                break;
            }
        }
        PortalMaxMbBox.Text = _config.PortalMaxZipMb.ToString();
        ExtraFoldersBox.Text = string.Join(Environment.NewLine, _config.ResourcesFolders);

        ExcludeBox.Text = string.Join(Environment.NewLine, _config.Exclude);
        EscrowExtraBox.Text = string.Join(Environment.NewLine, _config.EscrowZipExtraExcludes);
        WebPublishBox.Text = _config.EscrowWebPublishFolder;
        AutoEscrowDirsBox.Text = string.Join(Environment.NewLine, _config.AutoEscrowDirs);
        AutoEscrowAllBox.Text = string.Join(Environment.NewLine, _config.AutoEscrowAllExtensionsDirs);

        RunWebBuildBox.IsChecked = _config.RunWebBuildBeforeRelease;
        WebBuildCmdBox.Text = _config.WebBuildCommand;

        GitCommitBox.IsChecked = _config.GitCommitOnRelease;
        GitTagBox.IsChecked = _config.GitTagOnRelease;
        GitChangelogBox.IsChecked = _config.UseGitChangelog;

        DiscordBox.Text = SecretProtector.Unprotect(_config.DiscordWebhookProtected);
        GitHubRepoBox.Text = _config.GitHubRepo;
        GitHubTokenBox.Password = SecretProtector.Unprotect(_config.GitHubTokenProtected);
        GitHubOsBox.IsChecked = _config.UploadOsZipToGitHub;
        TebexPackageBox.Text = _config.TebexPackageId;
        TebexKeyBox.Password = SecretProtector.Unprotect(_config.TebexPrivateKeyProtected);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _config.Language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "tr";
        _config.WatchResourcesFolder = WatchFolderBox.IsChecked == true;
        _config.CheckForUpdates = CheckUpdatesBox.IsChecked == true;
        _config.AutoUploadPortal = AutoPortalBox.IsChecked == true;
        _config.VerifyZipAfterRelease = VerifyZipBox.IsChecked == true;
        if (long.TryParse(PortalMaxMbBox.Text.Trim(), out var mb))
            _config.PortalMaxZipMb = mb;

        _config.FollowSystemTheme = FollowSystemThemeBox.IsChecked == true;
        _config.AccentColor = (AccentBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        _config.ResourcesFolders = SplitLines(ExtraFoldersBox.Text);
        _config.Exclude = SplitLines(ExcludeBox.Text);
        _config.EscrowZipExtraExcludes = SplitLines(EscrowExtraBox.Text);
        _config.EscrowWebPublishFolder = WebPublishBox.Text.Trim();
        _config.AutoEscrowDirs = SplitLines(AutoEscrowDirsBox.Text);
        _config.AutoEscrowAllExtensionsDirs = SplitLines(AutoEscrowAllBox.Text);

        _config.RunWebBuildBeforeRelease = RunWebBuildBox.IsChecked == true;
        _config.WebBuildCommand = WebBuildCmdBox.Text.Trim();

        _config.GitCommitOnRelease = GitCommitBox.IsChecked == true;
        _config.GitTagOnRelease = GitTagBox.IsChecked == true;
        _config.UseGitChangelog = GitChangelogBox.IsChecked == true;

        _config.DiscordWebhookProtected = SecretProtector.Protect(DiscordBox.Text.Trim());
        _config.GitHubRepo = GitHubRepoBox.Text.Trim();
        _config.GitHubTokenProtected = SecretProtector.Protect(GitHubTokenBox.Password.Trim());
        _config.UploadOsZipToGitHub = GitHubOsBox.IsChecked == true;
        _config.TebexPackageId = TebexPackageBox.Text.Trim();
        _config.TebexPrivateKeyProtected = SecretProtector.Protect(TebexKeyBox.Password.Trim());

        _configService.Save(_config);
        DialogResult = true;
        Close();
    }

    private static List<string> SplitLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
