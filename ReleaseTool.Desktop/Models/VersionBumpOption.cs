namespace ReleaseTool.Desktop.Models;

public sealed class VersionBumpOption
{
    public required VersionBumpKind Kind { get; init; }
    public required string Label { get; init; }

    public override string ToString() => Label;
}
