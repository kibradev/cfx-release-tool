namespace ReleaseTool.Desktop.Models;

public sealed class ReleaseZipResult
{
    public required string Mode { get; init; }
    public required string ZipName { get; init; }
    public required string ZipPath { get; init; }
    public long Bytes { get; init; }
}
