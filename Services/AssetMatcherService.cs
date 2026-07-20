namespace ReleaseTool.Desktop.Services;

public static class AssetMatcherService
{
    public static int FindBestAssetId(string resourceName, IEnumerable<PortalAssetSearchItem> assets)
    {
        var list = assets.ToList();
        if (list.Count == 0)
            return 0;

        var exact = list.FirstOrDefault(a =>
            string.Equals(a.Name, resourceName, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact.Id;

        var normalized = Normalize(resourceName);
        var fuzzy = list
            .Select(a => new { Item = a, Score = Similarity(normalized, Normalize(a.Name)) })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return fuzzy is { Score: >= 0.6 } ? fuzzy.Item.Id : list[0].Id;
    }

    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
            return 0;

        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal))
            return 0.85;

        var common = a.Intersect(b).Count();
        return (2.0 * common) / (a.Length + b.Length);
    }
}
