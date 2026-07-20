using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class FileTreeFilterService
{
    public static void ApplyFilter(IEnumerable<FileTreeNode> roots, string? query)
    {
        var q = query?.Trim();
        foreach (var root in roots)
            ApplyNode(root, q);
    }

    private static bool ApplyNode(FileTreeNode node, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            node.IsVisible = true;
            if (node.IsDirectory)
            {
                foreach (var child in node.Children)
                    ApplyNode(child, query);
            }
            return true;
        }

        if (!node.IsDirectory)
        {
            var match = node.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || node.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
            node.IsVisible = match;
            return match;
        }

        var childVisible = false;
        foreach (var child in node.Children)
        {
            if (ApplyNode(child, query))
                childVisible = true;
        }

        var selfMatch = node.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || node.Path.Contains(query, StringComparison.OrdinalIgnoreCase);
        node.IsVisible = selfMatch || childVisible;
        if (childVisible)
            node.IsExpanded = true;

        return node.IsVisible;
    }
}
