using System.Windows;
using Microsoft.Web.WebView2.Core;
using ReleaseTool.Desktop.Models;
using ReleaseTool.Desktop.Services;

namespace ReleaseTool.Desktop.Views;

public partial class PortalLoginWindow : Window
{
    private readonly PortalUploadService _portalService = new();

    public PortalLoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitWebViewAsync();
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            StatusText.Text = "Tarayıcı başlatılıyor…";
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
            Browser.Source = new Uri("https://portal.cfx.re/");
            StatusText.Text = "portal.cfx.re açıldı — giriş yapın";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WebView2 başlatılamadı.\n\n{ex.Message}\n\n" +
                "WebView2 Runtime kur: https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                "Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private async void SaveSessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2 == null)
            return;

        SaveSessionButton.IsEnabled = false;
        StatusText.Text = "Cookie'ler alınıyor…";

        try
        {
            var all = new List<StoredPortalCookie>();
            foreach (var site in new[] { "https://portal.cfx.re/", "https://portal-api.cfx.re/", "https://forum.cfx.re/" })
            {
                var list = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(site);
                foreach (var c in list)
                {
                    all.Add(new StoredPortalCookie
                    {
                        Name = c.Name,
                        Value = c.Value,
                        Domain = c.Domain,
                        Path = c.Path
                    });
                }
            }

            all = all
                .GroupBy(c => $"{c.Domain}|{c.Name}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (all.Count == 0)
            {
                MessageBox.Show("Cookie bulunamadı. Önce portalda giriş yapın.", "Portal", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _portalService.SavePortalSession(all);

            // Doğrula
            await _portalService.TestAuthenticationAsync();
            MessageBox.Show(
                $"Portal oturumu kaydedildi ({all.Count} cookie).\n\nArtık 'Bağlantıyı test et' çalışmalı.",
                "Başarılı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Oturum kaydedilemedi", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Hata — portalda giriş yaptığınızdan emin olun";
        }
        finally
        {
            SaveSessionButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
