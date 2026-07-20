namespace ReleaseTool.Desktop.Models;

public sealed class ReleasePipelineOptions
{
    public string? ManualVersion { get; init; }
    public bool SkipWebBuild { get; init; }
    public bool SkipPreChecks { get; init; }
    public bool SkipPostSteps { get; init; }
    public bool AutoUploadPortal { get; init; }
}
