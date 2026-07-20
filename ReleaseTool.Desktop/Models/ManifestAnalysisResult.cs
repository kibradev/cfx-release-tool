namespace ReleaseTool.Desktop.Models;

public sealed class ManifestAnalysisResult
{
    public List<string> MissingFiles { get; init; } = [];
    public List<string> ReferencedFiles { get; init; } = [];
    public string? WebBuildWarning { get; init; }
    public bool HasIssues => MissingFiles.Count > 0 || WebBuildWarning != null;
}
