namespace ReleaseTool.Desktop.Services;

public sealed class FolderWatchService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Action? _onChanged;
    private DateTime _lastFire = DateTime.MinValue;

    public void Start(string folder, Action onChanged)
    {
        Stop();
        if (!Directory.Exists(folder))
            return;

        _onChanged = onChanged;
        _watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        _watcher.Changed += (_, _) => DebouncedFire();
        _watcher.Created += (_, _) => DebouncedFire();
        _watcher.Deleted += (_, _) => DebouncedFire();
        _watcher.Renamed += (_, _) => DebouncedFire();
    }

    private void DebouncedFire()
    {
        if ((DateTime.UtcNow - _lastFire).TotalSeconds < 2)
            return;

        _lastFire = DateTime.UtcNow;
        _onChanged?.Invoke();
    }

    public void Stop()
    {
        if (_watcher == null)
            return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose() => Stop();
}
