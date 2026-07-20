using CommunityToolkit.Mvvm.ComponentModel;

namespace ReleaseTool.Desktop.Models;

public partial class FileTreeNode : ObservableObject
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required bool IsDirectory { get; init; }
    public FileTreeNode? Parent { get; set; }
    public List<FileTreeNode> Children { get; init; } = [];

    [ObservableProperty]
    private bool? _isChecked;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isAutoSuggested;

    [ObservableProperty]
    private bool _isVisible = true;

    public bool HasChildren => IsDirectory && Children.Count > 0;

    public IEnumerable<FileTreeNode> DescendantFiles()
    {
        if (!IsDirectory)
        {
            yield return this;
            yield break;
        }

        foreach (var child in Children)
        {
            foreach (var file in child.DescendantFiles())
                yield return file;
        }
    }
}
