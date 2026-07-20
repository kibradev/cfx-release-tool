using System.Net;
using System.Text.Json;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class PortalSessionStore
{
    public static string Serialize(IEnumerable<StoredPortalCookie> cookies)
    {
        return JsonSerializer.Serialize(cookies.ToList());
    }

    public static List<StoredPortalCookie> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<StoredPortalCookie>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void ApplyToContainer(CookieContainer container, IEnumerable<StoredPortalCookie> cookies)
    {
        foreach (var c in cookies)
        {
            if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Domain))
                continue;

            try
            {
                var domain = c.Domain.StartsWith('.') ? c.Domain : $".{c.Domain.TrimStart('.')}";
                var uri = new Uri($"https://{domain.TrimStart('.')}");
                container.Add(uri, new Cookie(c.Name, c.Value, c.Path ?? "/", domain)
                {
                    HttpOnly = true,
                    Secure = true
                });
            }
            catch
            {
                // skip
            }
        }
    }

    public static string Protect(IEnumerable<StoredPortalCookie> cookies) =>
        SecretProtector.Protect(Serialize(cookies));

    public static List<StoredPortalCookie> Unprotect(string protectedText) =>
        Deserialize(SecretProtector.Unprotect(protectedText));
}
