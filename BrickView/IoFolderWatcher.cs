using System.IO;

namespace BrickView;

public class IoFolderWatcher : IDisposable
{
    private FileSystemWatcher? watcher;

    public event EventHandler? FolderChanged;

    public void Start(string folder)
    {
        Stop();

        watcher = new FileSystemWatcher
        {
            Path = folder,
            Filter = "*.io",
            NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        watcher.Created += OnFileSystemChanged;
        watcher.Changed += OnFileSystemChanged;
        watcher.Deleted += OnFileSystemChanged;
        watcher.Renamed += OnFileSystemRenamed;
        watcher.Error += OnWatcherError;
    }

    public void Stop()
    {
        if (watcher is null)
        {
            return;
        }

        watcher.EnableRaisingEvents = false;

        watcher.Created -= OnFileSystemChanged;
        watcher.Changed -= OnFileSystemChanged;
        watcher.Deleted -= OnFileSystemChanged;
        watcher.Renamed -= OnFileSystemRenamed;
        watcher.Error -= OnWatcherError;

        watcher.Dispose();

        watcher = null;
    }

    private void OnFileSystemChanged(
        object sender,
        FileSystemEventArgs e)
    {
        FolderChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnFileSystemRenamed(
        object sender,
        RenamedEventArgs e)
    {
        FolderChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnWatcherError(
        object sender,
        ErrorEventArgs e)
    {
        FolderChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
    }
}