namespace ReleaseTool.Desktop.Models;

public sealed class VersionParts
{
    public required string Full { get; init; }
    public int Major { get; init; }
    public int Minor { get; init; }
    public int Patch { get; init; }
}
