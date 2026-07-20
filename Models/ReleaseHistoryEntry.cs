namespace ReleaseTool.Desktop.Models;

public sealed class ReleaseHistoryEntry
{
    public string ResourceName { get; set; } = "";
    public string ResourcePath { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public long EscrowBytes { get; set; }
    public long OsBytes { get; set; }
    public string EscrowZipPath { get; set; } = "";
    public string OsZipPath { get; set; } = "";
}
