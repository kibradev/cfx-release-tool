namespace ReleaseTool.Desktop.Helpers;

public static class PathHelper
{
    public static string ToPosix(string path) => path.Replace('\\', '/');
}
