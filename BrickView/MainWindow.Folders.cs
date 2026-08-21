// -----------------------------------------------------------------------------
// MainWindow.Folders.cs
//
// Contains MainWindow's folder-selection, folder-loading, filesystem-refresh,
// model-list synchronization and FileSystemWatcher refresh orchestration.
// -----------------------------------------------------------------------------

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace BrickView;

public partial class MainWindow {

    /// <summary>
    /// Opens the folder-selection dialog and loads the selected folder into BrickView.
    /// </summary>
    /// <param name="sender">The button that raised the event.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private async void SelectFolder_Click(
        object sender,
        RoutedEventArgs e) {
        OpenFolderDialog dialog =
            new OpenFolderDialog {
                Title = "Pick Folder"
            };

        bool? result =
            dialog.ShowDialog();

        if (result != true) {
            return;
        }

        string folder =
            dialog.FolderName;

        if (string.IsNullOrWhiteSpace(
                folder)) {
            return;
        }

        currentFolder =
            folder;

        NoFolderSelectedText.Visibility =
            Visibility.Collapsed;

        folderWatcher.Start(
            folder);

        FolderText.Text =
            folder;

        await LoadIoFilesAsync(
            folder);
    }

    /// <summary>
    /// Refreshes the currently selected folder and applies any detected file changes.
    /// </summary>
    /// <param name="sender">The button that raised the event.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private async void RefreshView_Click(
        object sender,
        RoutedEventArgs e) {
        CancellationTokenSource refreshCancellation =
            StartFolderRefresh();

        try {
            await RefreshCurrentFolderAsync(
                refreshCancellation.Token);
        }
        catch (OperationCanceledException) {
            // Expected when another refresh supersedes this refresh.
        }
        finally {
            DisposeCurrentFolderRefresh(
                refreshCancellation);
        }
    }

    /// <summary>
    /// Loads all top-level .io files from the specified folder into the model list,
    /// starts asynchronous identity resolution and applies the current sort and filter.
    /// </summary>
    /// <param name="folder">The folder containing the BrickView model files.</param>
    /// <returns>A completed task after the initial file list has been prepared.</returns>
    private async Task LoadIoFilesAsync(
        string folder) {
        modelIdentityCancellation?.Cancel();

        modelIdentityCancellation?.Dispose();

        modelIdentityCancellation =
            new CancellationTokenSource();

        allFileItems.Clear();

        FileList.Items.Clear();

        NoResultsText.Visibility =
            Visibility.Collapsed;

        ModelCountText.Text =
            "0 models";

        string[] files =
            await GetIoFilesAsync(
                folder);

        foreach (string file in files) {
            AddFileListItem(
                file);
        }

        SortAllFileItems();

        ApplySearchFilter();
    }

    /// <summary>
    /// Compares the current folder with the existing model list and applies additions,
    /// removals, modifications and renames before refreshing the visible list.
    /// </summary>
    /// <returns>A task that completes when the folder comparison and UI refresh finish.</returns>
    /// <param name="cancellationToken">Token used to cancel a superseded folder refresh.</param>
    private async Task RefreshCurrentFolderAsync(
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(
                currentFolder)) {
            return;
        }

        if (!Directory.Exists(
                currentFolder)) {
            MessageBox.Show(
                "The selected folder no longer exists.",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            currentFolder = null;

            folderWatcher.Stop();

            allFileItems.Clear();

            FileList.Items.Clear();

            NoResultsText.Visibility =
                Visibility.Collapsed;

            NoFolderSelectedText.Visibility =
                Visibility.Visible;

            FolderText.Text =
                string.Empty;

            ModelCountText.Text =
                "0 models";

            return;
        }

        NoFolderSelectedText.Visibility =
            Visibility.Collapsed;

        string[] currentFiles =
            await GetIoFilesAsync(
                currentFolder);

        cancellationToken.ThrowIfCancellationRequested();

        List<IoFileListItem> existingItems =
            allFileItems.ToList();

        FolderDiff diff =
            await folderDiffService.CompareAsync(
                existingItems,
                currentFiles,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        foreach (FileChange change
                 in diff.Changes) {
            switch (change.ChangeType) {
                case FileChangeType.Added:

                    AddFileListItem(
                        change.FilePath);

                    break;

                case FileChangeType.Removed:

                    RemoveFileListItem(
                        change.FilePath);

                    break;

                case FileChangeType.Modified:

                    UpdateModifiedFile(
                        change.FilePath);

                    break;

                case FileChangeType.Renamed:

                    UpdateRenamedFile(
                        change.PreviousFilePath,
                        change.FilePath);

                    break;

                case FileChangeType.Unchanged:

                    break;
            }
        }

        SortAllFileItems();

        ApplySearchFilter();
    }

    /// <summary>
    /// Enumerates the top-level .io files in a folder on a background thread and
    /// returns them sorted by file name.
    /// </summary>
    /// <param name="folder">The folder containing the BrickView model files.</param>
    /// <returns>A task containing the sorted .io file paths.</returns>
    /// <remarks>
    /// Directory enumeration is deliberately kept off the WPF UI thread because
    /// filesystem access can be noticeably slow for large, network or removable
    /// folders. The returned paths are later consumed by UI-thread model updates.
    /// </remarks>
    private static Task<string[]> GetIoFilesAsync(
        string folder) {
        return Task.Run(
            () =>
                Directory.GetFiles(
                    folder,
                    "*.io",
                    SearchOption.TopDirectoryOnly)
                    .OrderBy(
                        file => Path.GetFileName(file),
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray());
    }

    /// <summary>
    /// Creates and registers a model-list item for an existing .io file and starts
    /// asynchronous model-identity resolution for that item.
    /// </summary>
    /// <param name="filePath">The full path of the .io file to add.</param>
    private void AddFileListItem(
        string filePath) {
        if (!File.Exists(
                filePath)) {
            return;
        }

        FileInfo fileInfo =
            new FileInfo(
                filePath);

        string fileName =
            Path.GetFileNameWithoutExtension(
                filePath);

        IoFileListItem item =
            new IoFileListItem(
                fileName,
                filePath,
                fileInfo.Length,
                fileInfo.CreationTimeUtc,
                fileInfo.LastWriteTimeUtc,
                null,
                null);

        allFileItems.Add(
            item);

        CancellationToken cancellationToken =
            modelIdentityCancellation?.Token ??
            CancellationToken.None;

        // Identity resolution is deliberately started in the background.
        // Creating the visible file item therefore does not wait for a
        // Windows file-system identity lookup.
        _ = LoadModelIdentityAsync(
            item,
            cancellationToken);
    }

    /// <summary>
    /// Resolves a model's stable Windows file identity asynchronously and then loads
    /// its tags and Favorite state from the shared tag service.
    /// </summary>
    /// <param name="item">The model-list item whose identity should be resolved.</param>
    /// <param name="cancellationToken">Token used to cancel obsolete identity lookups.</param>
    /// <returns>A task representing the asynchronous identity-resolution operation.</returns>
    private async Task LoadModelIdentityAsync(
        IoFileListItem item,
        CancellationToken cancellationToken) {
        try {
            ModelIdentity? modelIdentity =
                await Task.Run(
                    () =>
                        fileIdentityProvider.TryGetIdentity(
                            item.FilePath),
                    cancellationToken);

            if (cancellationToken.IsCancellationRequested ||
                modelIdentity is null) {
                return;
            }

            if (!allFileItems.Contains(item)) {
                return;
            }

            item.SetModelIdentity(
                modelIdentity);

            item.SetTags(
                tagService.GetTags(
                    modelIdentity));

            item.IsFavorite =
                tagService.IsFavorite(
                    modelIdentity);

            // A plain model list does not depend on model identity, tags or
            // favorite state. Avoid rebuilding the ListBox for every model
            // while the initial identity lookups complete. Search is refreshed
            // only when an active search or favorite filter actually depends
            // on this information.
            if (HasActiveSearchFilter()) {
                RequestSearchRefresh();
            }
        }
        catch (OperationCanceledException) {
            // Expected when a folder reload or application shutdown cancels
            // an outstanding identity lookup.
        }
    }

    /// <summary>
    /// Updates an existing model-list item after its backing file has been renamed,
    /// preserving the existing item so its model identity and associated state remain intact.
    /// </summary>
    /// <param name="previousFilePath">The file path before the rename.</param>
    /// <param name="newFilePath">The file path after the rename.</param>
    private void UpdateRenamedFile(
        string? previousFilePath,
        string newFilePath) {
        if (string.IsNullOrWhiteSpace(
                previousFilePath)) {
            return;
        }

        IoFileListItem? item =
            allFileItems
                .FirstOrDefault(
                    existingItem =>
                        string.Equals(
                            existingItem.FilePath,
                            previousFilePath,
                            StringComparison.OrdinalIgnoreCase));

        if (item is null ||
            !File.Exists(
                newFilePath)) {
            return;
        }

        FileInfo fileInfo =
            new FileInfo(
                newFilePath);

        item.UpdateFilePath(
            newFilePath,
            fileInfo.Length,
            fileInfo.CreationTimeUtc,
            fileInfo.LastWriteTimeUtc);

        item.InvalidateThumbnail();

        item.InvalidateMetadata();

        CancellationToken cancellationToken =
            modelIdentityCancellation?.Token ??
            CancellationToken.None;

        _ = LoadModelIdentityAsync(
            item,
            cancellationToken);
    }

    /// <summary>
    /// Removes the model-list item whose file path matches the specified path.
    /// </summary>
    /// <param name="filePath">The path of the model file to remove.</param>
    private void RemoveFileListItem(
        string filePath) {
        IoFileListItem? item =
            allFileItems
                .FirstOrDefault(
                    existingItem =>
                        string.Equals(
                            existingItem.FilePath,
                            filePath,
                            StringComparison.OrdinalIgnoreCase));

        if (item is not null) {
            allFileItems.Remove(
                item);
        }
    }

    /// <summary>
    /// Refreshes file information for a modified model and invalidates its thumbnail
    /// and metadata so the updated file can be represented correctly.
    /// </summary>
    /// <param name="filePath">The path of the modified model file.</param>
    private void UpdateModifiedFile(
        string filePath) {
        IoFileListItem? item =
            allFileItems
                .FirstOrDefault(
                    existingItem =>
                        string.Equals(
                            existingItem.FilePath,
                            filePath,
                            StringComparison.OrdinalIgnoreCase));

        if (item is null) {
            return;
        }

        if (!File.Exists(
                filePath)) {
            return;
        }

        FileInfo fileInfo =
            new FileInfo(
                filePath);

        item.UpdateFileInfo(
            fileInfo.Length,
            fileInfo.CreationTimeUtc,
            fileInfo.LastWriteTimeUtc);

        item.InvalidateThumbnail();

        item.InvalidateMetadata();
    }

    /// <summary>
    /// Marshals a file-system change notification to the WPF dispatcher so the folder
    /// refresh can be scheduled safely on the UI thread.
    /// </summary>
    /// <param name="sender">The folder watcher that raised the event.</param>
    /// <param name="e">Event data supplied by the folder watcher.</param>
    private void FolderWatcher_FolderChanged(
        object? sender,
        EventArgs e) {
        Dispatcher.InvokeAsync(
            ScheduleFolderRefresh);
    }

    /// <summary>
    /// Cancels any pending folder refresh and starts a new debounced refresh operation.
    /// </summary>
    private void ScheduleFolderRefresh() {
        CancellationTokenSource refreshCancellation =
            StartFolderRefresh();

        _ = DebouncedFolderRefreshAsync(
            refreshCancellation);
    }

    /// <summary>
    /// Cancels the current folder refresh and creates a new cancellation source for
    /// the refresh that should now be treated as current.
    /// </summary>
    /// <returns>The cancellation source owned by the new folder refresh.</returns>
    private CancellationTokenSource StartFolderRefresh() {
        folderRefreshCancellation?.Cancel();

        CancellationTokenSource refreshCancellation =
            new CancellationTokenSource();

        folderRefreshCancellation =
            refreshCancellation;

        return refreshCancellation;
    }

    /// <summary>
    /// Disposes a folder-refresh cancellation source after its associated operation
    /// has completed, but only clears the shared reference when it is still current.
    /// </summary>
    /// <param name="refreshCancellation">The cancellation source being completed.</param>
    private void DisposeCurrentFolderRefresh(
        CancellationTokenSource refreshCancellation) {
        if (ReferenceEquals(
                folderRefreshCancellation,
                refreshCancellation)) {
            folderRefreshCancellation =
                null;
        }

        refreshCancellation.Dispose();
    }

    /// <summary>
    /// Waits briefly after a file-system event before refreshing the current folder,
    /// allowing bursts of file-system notifications to be processed as one update.
    /// </summary>
    /// <param name="refreshCancellation">Cancellation source owned by the debounced refresh.</param>
    /// <returns>A task representing the debounced refresh operation.</returns>
    private async Task DebouncedFolderRefreshAsync(
        CancellationTokenSource refreshCancellation) {
        try {
            await Task.Delay(
                TimeSpan.FromMilliseconds(300),
                refreshCancellation.Token);

            await RefreshCurrentFolderAsync(
                refreshCancellation.Token);
        }
        catch (OperationCanceledException) {
            // Expected when a new file system event resets
            // the debounce timer or supersedes an active refresh.
        }
        finally {
            DisposeCurrentFolderRefresh(
                refreshCancellation);
        }
    }
}