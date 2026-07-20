namespace ReleaseTool.Desktop.Models;

public sealed class EscrowPreviewSummary
{
    public int TotalFiles { get; init; }
    public int OpenFiles { get; init; }
    public int EscrowedFiles { get; init; }
    public long EstimatedEscrowBytes { get; init; }
    public long EstimatedOsBytes { get; init; }
    public List<string> OpenPaths { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}
