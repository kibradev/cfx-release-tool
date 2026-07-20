using CommunityToolkit.Mvvm.ComponentModel;

namespace ReleaseTool.Desktop.Models;

public partial class ResourceInfo : ObservableObject
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Version { get; init; }
    public bool HasManifest { get; init; }

    [ObservableProperty]
    private bool _isBatchSelected;
}
