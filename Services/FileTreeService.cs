using ReleaseTool.Desktop.Helpers;
using ReleaseTool.Desktop.Models;

namespace ReleaseTool.Desktop.Services;

public static class FileTreeService
{
    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".github", ".vscode", ".idea", ".cache", "cache", "tmp", "temp", "logs"
    };

    public static List<FileTreeNode> BuildTree(string resourcePath)
    {
        var root = Path.GetFullPath(resourcePath);
        return BuildChildren(root, "");
    }

    private static List<FileTreeNode> BuildChildren(string root, string prefix)
    {
        var nodes = new List<FileTreeNode>();

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(root);
        }
        catch
        {
            return nodes;
        }

        foreach (var fullPath in entries)
        {
            var name = Path.GetFileName(fullPath);
            if (SkipDirNames.Contains(name))
                continue;

            if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                continue;

            var rel = string.IsNullOrEmpty(prefix) ? name : PathHelper.ToPosix(Path.Combine(prefix, name));
            var isDir = Directory.Exists(fullPath);

            var node = new FileTreeNode
            {
                Name = name,
                Path = rel,
                IsDirectory = isDir
            };

            if (isDir)
            {
                foreach (var child in BuildChildren(fullPath, rel))
                {
                    child.Parent = node;
                    node.Children.Add(child);
                }
            }

            nodes.Add(node);
        }

        return nodes
            .OrderBy(n => n.IsDirectory ? 0 : 1)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> CollectAllFilePaths(IEnumerable<FileTreeNode> nodes)
    {
        var output = new List<string>();
        foreach (var node in nodes)
            CollectFiles(node, output);
        return output;
    }

    private static void CollectFiles(FileTreeNode node, List<string> output)
    {
        if (!node.IsDirectory)
        {
            output.Add(node.Path);
            return;
        }

        foreach (var child in node.Children)
            CollectFiles(child, output);
    }

    public static Dictionary<string, List<string>> BuildFolderToDescendantFiles(IEnumerable<FileTreeNode> nodes)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        List<string> Visit(FileTreeNode node)
        {
            if (!node.IsDirectory)
                return [node.Path];

            var acc = new List<string>();
            foreach (var child in node.Children)
                acc.AddRange(Visit(child));

            map[node.Path] = acc;
            return acc;
        }

        foreach (var root in nodes)
            Visit(root);

        return map;
    }
}
