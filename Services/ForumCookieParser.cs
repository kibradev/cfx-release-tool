using System.Net;

namespace ReleaseTool.Desktop.Services;

public static class ForumCookieParser
{
    private static readonly HashSet<string> ImportantCookies = new(StringComparer.OrdinalIgnoreCase)
    {
        "_t", "_forum_session", "GAESA", "_discourse_session"
    };

    /// <summary>
    /// Tam cookie satırı veya yalnızca _t değeri kabul eder.
    /// </summary>
    public static Dictionary<string, string> Parse(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var text = input.Trim();

        if (string.IsNullOrEmpty(text))
            return result;

        // Yalnızca _t değeri (içinde ; yok veya _t= ile başlamıyor)
        if (!text.Contains('_') || (!text.Contains(';') && !text.Contains('=')))
        {
            result["_t"] = DecodeCookieValue(text);
            return result;
        }

        // _t=... tek parça
        if (text.StartsWith("_t=", StringComparison.OrdinalIgnoreCase) && !text.Contains(';'))
        {
            result["_t"] = DecodeCookieValue(text[3..]);
            return result;
        }

        // Tam cookie header: a=b; c=d; ...
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var name = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();

            if (ImportantCookies.Contains(name) || name.StartsWith('_'))
                result[name] = DecodeCookieValue(value);
        }

        return result;
    }

    private static string DecodeCookieValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        try
        {
            // DevTools URL-encoded verir; .NET Cookie decode edilmiş bekler
            return Uri.UnescapeDataString(value);
        }
        catch
        {
            return value;
        }
    }

    public static void ApplyToContainer(CookieContainer container, Dictionary<string, string> cookies)
    {
        var domains = new[]
        {
            "https://forum.cfx.re",
            "https://cfx.re",
            "https://portal.cfx.re"
        };

        foreach (var (name, value) in cookies)
        {
            foreach (var domain in domains)
            {
                try
                {
                    container.Add(new Uri(domain), new Cookie(name, value)
                    {
                        HttpOnly = true,
                        Secure = true,
                        Path = "/"
                    });
                }
                catch
                {
                    // skip invalid
                }
            }
        }
    }
}
