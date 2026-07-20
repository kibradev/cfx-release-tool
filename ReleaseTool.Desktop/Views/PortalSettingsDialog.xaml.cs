using System.Diagnostics;
using System.Windows;
using ReleaseTool.Desktop.Models;
using ReleaseTool.Desktop.Services;

namespace ReleaseTool.Desktop.Views;

public partial class PortalSettingsDialog : Window
{
    private readonly ConfigService _configService = new();
    private readonly PortalUploadService _portalService = new();
    private readonly string? _resourceName;

    public PortalSettingsDialog(string? resourceName = null, int currentAssetId = 0)
    {
        InitializeComponent();
        _resourceName = resourceName;
        AssetIdBox.Text = currentAssetId > 0 ? currentAssetId.ToString() : "";
        ResourceNameBlock.Text = resourceName ?? "(genel ayarlar)";
        LoadCookie();
    }

    private void LoadCookie()
    {
        var config = _configService.Load();
        CookieBox.Password = SecretProtector.Unprotect(config.ForumCookieProtected);
    }

    private string GetCookie() => CookieBox.Password.Trim();

    private async void BrowserLoginButton_Click(object sender, RoutedEventArgs e)
    {
        var win = new PortalLoginWindow { Owner = this };
        if (win.ShowDialog() == true)
        {
            StatusText.Text = "Portal oturumu kaydedildi ✓";
            try
            {
                await _portalService.TestAuthenticationAsync();
                MessageBox.Show("Bağlantı başarılı! Upload kullanılabilir.", "Portal", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Test", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var cookie = GetCookie();

        TestButton.IsEnabled = false;
        StatusText.Text = "Bağlantı test ediliyor…";
        try
        {
            var ok = await _portalService.TestAuthenticationAsync(
                string.IsNullOrEmpty(cookie) ? null : cookie);
            StatusText.Text = ok ? "Bağlantı başarılı ✓" : "Bağlantı başarısız";
            MessageBox.Show(
                ok ? "Portal oturumu açıldı. Upload kullanılabilir." : "Cookie geçersiz veya süresi dolmuş.",
                "Portal test",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Hata";
            MessageBox.Show(ex.Message, "Portal test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var cookie = GetCookie();
        if (string.IsNullOrEmpty(cookie))
        {
            MessageBox.Show("Önce cookie girin.", "Portal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query) && !string.IsNullOrEmpty(_resourceName))
            query = _resourceName;

        if (string.IsNullOrEmpty(query))
        {
            MessageBox.Show("Arama adı girin.", "Portal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SearchButton.IsEnabled = false;
        StatusText.Text = "Asset aranıyor…";
        try
        {
            var items = await _portalService.SearchAssetsAsync(cookie, query);
            if (items.Count == 0)
            {
                MessageBox.Show("Asset bulunamadı.", "Portal", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var list = string.Join("\n", items.Take(15).Select(i => $"{i.Id} — {i.Name}"));
            var pick = MessageBox.Show(
                list + "\n\nİlk sonucu kullanmak ister misiniz?",
                "Bulunan asset'ler",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (pick == MessageBoxResult.Yes)
            {
                AssetIdBox.Text = items[0].Id.ToString();
                SearchBox.Text = items[0].Name;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Arama hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SearchButton.IsEnabled = true;
            StatusText.Text = "";
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var config = _configService.Load();
        config.ForumCookieProtected = SecretProtector.Protect(GetCookie());

        if (!string.IsNullOrEmpty(_resourceName) && int.TryParse(AssetIdBox.Text.Trim(), out var assetId) && assetId > 0)
        {
            config.PortalAssets.RemoveAll(p =>
                string.Equals(p.ResourceName, _resourceName, StringComparison.OrdinalIgnoreCase));
            config.PortalAssets.Add(new PortalAssetMapping
            {
                ResourceName = _resourceName,
                AssetId = assetId,
                PortalAssetName = SearchBox.Text.Trim()
            });
        }

        _configService.Save(config);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://forum.cfx.re",
            UseShellExecute = true
        });

        MessageBox.Show(
            "1. forum.cfx.re → giriş yap\n" +
            "2. F12 → Network → forum.cfx.re → Headers\n" +
            "3. Request Headers → cookie: satırının TAMAMINI kopyala\n" +
            "   (_ga=...; _t=...; _forum_session=... hepsi)\n" +
            "4. Buraya yapıştır → Bağlantıyı test et\n\n" +
            "Sadece _t yetmeyebilir — tam satır daha güvenilir.\n" +
            "Cookie expire olunca forumdan çıkış → tekrar giriş → yeni al.",
            "Cookie nasıl alınır?",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
