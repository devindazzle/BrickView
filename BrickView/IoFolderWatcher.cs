// -----------------------------------------------------------------------------
// IoFolderWatcher.cs
//
// Monitors a BrickView model folder for changes to .io files.
//
// The watcher reports file creation, modification, deletion and rename events
// through the FolderChanged event. BrickView's folder-diff logic is responsible
// for determining the actual model-level change represented by those events.
//
// The watcher does not perform file comparisons or update the UI directly.
// Its sole responsibility is to observe the file system and notify its caller
// that the monitored folder may have changed.
// -----------------------------------------------------------------------------

using System.IO;

namespace BrickView;

/// <summary>
/// Monitors a folder for changes to BrickLink Studio .io model files.
/// </summary>
public sealed class IoFolderWatcher : IDisposable {
    private FileSystemWatcher? watcher;

    /// <summary>
    /// Raised when the monitored folder may contain a changed .io file.
    /// </summary>
    public event EventHandler? FolderChanged;

    /// <summary>
    /// Starts monitoring the specified folder.
    ///
    /// Any existing watcher is stopped before the new watcher is created.
    /// </summary>
    /// <param name="folder">
    /// The folder containing the .io model files to monitor.
    /// </param>
    public void Start(
        string folder) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            folder);

        Stop();

        watcher =
            new FileSystemWatcher {
                Path =
                    folder,

                Filter =
                    "*.io",

                NotifyFilter =
                    NotifyFilters.FileName |
                    NotifyFilters.LastWrite |
                    NotifyFilters.Size,

                IncludeSubdirectories =
                    false,

                EnableRaisingEvents =
                    true
            };

        watcher.Created +=
            OnFileSystemChanged;

        watcher.Changed +=
            OnFileSystemChanged;

        watcher.Deleted +=
            OnFileSystemChanged;

        watcher.Renamed +=
            OnFileSystemRenamed;

        watcher.Error +=
            OnWatcherError;
    }

    /// <summary>
    /// Stops monitoring the current folder and releases the underlying
    /// FileSystemWatcher.
    /// </summary>
    public void Stop() {
        if (watcher is null) {
            return;
        }

        watcher.EnableRaisingEvents =
            false;

        watcher.Created -=
            OnFileSystemChanged;

        watcher.Changed -=
            OnFileSystemChanged;

        watcher.Deleted -=
            OnFileSystemChanged;

        watcher.Renamed -=
            OnFileSystemRenamed;

        watcher.Error -=
            OnWatcherError;

        watcher.Dispose();

        watcher =
            null;
    }

    /// <summary>
    /// Handles file creation, modification and deletion notifications.
    /// </summary>
    private void OnFileSystemChanged(
        object sender,
        FileSystemEventArgs e) {
        FolderChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    /// <summary>
    /// Handles file rename notifications.
    /// </summary>
    private void OnFileSystemRenamed(
        object sender,
        RenamedEventArgs e) {
        FolderChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    /// <summary>
    /// Handles errors raised by FileSystemWatcher.
    ///
    /// The caller is notified in the same way as for a normal file-system
    /// event so it can perform a full folder comparison and recover to a
    /// consistent state.
    /// </summary>
    private void OnWatcherError(
        object sender,
        ErrorEventArgs e) {
        FolderChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    /// <summary>
    /// Stops monitoring and releases all resources owned by the watcher.
    /// </summary>
    public void Dispose() {
        Stop();
    }
}