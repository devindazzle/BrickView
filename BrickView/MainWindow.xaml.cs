using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BrickView;

public partial class MainWindow : Window
{
    private readonly ThumbnailLoader thumbnailLoader;

    private readonly FolderDiffService folderDiffService;

    private readonly IoFolderWatcher folderWatcher;

    private string? currentFolder;

    private CancellationTokenSource? folderRefreshCancellation;

    public MainWindow()
    {
        InitializeComponent();

        thumbnailLoader =
            new ThumbnailLoader();

        folderDiffService =
            new FolderDiffService();

        folderWatcher =
            new IoFolderWatcher();

        folderWatcher.FolderChanged +=
            FolderWatcher_FolderChanged;

        FileList.AddHandler(
            VirtualizingWrapPanel.ViewportChangedEvent,
            new RoutedEventHandler(
                FileList_ViewportChanged));
    }

    private async void SelectFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new OpenFolderDialog
        {
            Title = "Pick Folder"
        };

        bool? result = dialog.ShowDialog();

        if (result != true)
        {
            return;
        }

        string folder = dialog.FolderName;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        currentFolder = folder;

        folderWatcher.Start(folder);

        FolderText.Text = folder;

        await LoadIoFilesAsync(folder);
    }

    private async void RefreshView_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshCurrentFolderAsync();
    }

    private async Task LoadIoFilesAsync(
        string folder)
    {
        FileList.Items.Clear();

        string[] files = Directory.GetFiles(
            folder,
            "*.io",
            SearchOption.TopDirectoryOnly)
            .OrderBy(
                file => Path.GetFileName(file),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string file in files)
        {
            AddFileListItem(file);
        }

        await Task.CompletedTask;
    }

    private async Task RefreshCurrentFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(currentFolder))
        {
            return;
        }

        if (!Directory.Exists(currentFolder))
        {
            MessageBox.Show(
                "The selected folder no longer exists.",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            currentFolder = null;

            folderWatcher.Stop();

            FolderText.Text = string.Empty;

            return;
        }

        string[] currentFiles = Directory.GetFiles(
            currentFolder,
            "*.io",
            SearchOption.TopDirectoryOnly)
            .OrderBy(
                file => Path.GetFileName(file),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<IoFileListItem> existingItems =
            FileList.Items
                .OfType<IoFileListItem>()
                .ToList();

        FolderDiff diff =
            folderDiffService.Compare(
                existingItems,
                currentFiles);

        foreach (FileChange change in diff.Changes)
        {
            switch (change.ChangeType)
            {
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

                case FileChangeType.Unchanged:

                    break;
            }
        }

        SortFileList();

        await Task.CompletedTask;
    }

    private void AddFileListItem(
        string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        FileInfo fileInfo =
            new FileInfo(filePath);

        string fileName =
            Path.GetFileNameWithoutExtension(
                filePath);

        IoFileListItem item =
            new IoFileListItem(
                fileName,
                filePath,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc,
                null,
                null);

        FileList.Items.Add(item);
    }

    private void RemoveFileListItem(
        string filePath)
    {
        IoFileListItem? item =
            FileList.Items
                .OfType<IoFileListItem>()
                .FirstOrDefault(
                    existingItem =>
                        string.Equals(
                            existingItem.FilePath,
                            filePath,
                            StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            FileList.Items.Remove(item);
        }
    }

    private void UpdateModifiedFile(
        string filePath)
    {
        IoFileListItem? item =
            FileList.Items
                .OfType<IoFileListItem>()
                .FirstOrDefault(
                    existingItem =>
                        string.Equals(
                            existingItem.FilePath,
                            filePath,
                            StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            return;
        }

        FileInfo fileInfo =
            new FileInfo(filePath);

        item.UpdateFileInfo(
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc);

        item.InvalidateThumbnail();
    }

    private void SortFileList()
    {
        List<IoFileListItem> sortedItems =
            FileList.Items
                .OfType<IoFileListItem>()
                .OrderBy(
                    item => item.FileName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        FileList.Items.Clear();

        foreach (IoFileListItem item in sortedItems)
        {
            FileList.Items.Add(item);
        }
    }

    private void FileList_ViewportChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (e is not ViewportChangedEventArgs viewportEventArgs)
        {
            return;
        }

        int firstVisibleIndex =
            viewportEventArgs.FirstVisibleIndex;

        int lastVisibleIndex =
            viewportEventArgs.LastVisibleIndex;

        int itemCount =
            FileList.Items.Count;

        if (itemCount == 0)
        {
            return;
        }

        int clampedFirstIndex =
            Math.Max(
                0,
                Math.Min(
                    firstVisibleIndex,
                    itemCount - 1));

        int clampedLastIndex =
            Math.Max(
                clampedFirstIndex,
                Math.Min(
                    lastVisibleIndex,
                    itemCount - 1));

        for (
            int index = clampedFirstIndex;
            index <= clampedLastIndex;
            index++)
        {
            if (FileList.Items[index]
                is IoFileListItem item)
            {
                _ = thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Visible);
            }
        }

        const int preloadCount = 8;

        int preloadStart =
            clampedLastIndex + 1;

        int preloadEnd =
            Math.Min(
                itemCount - 1,
                preloadStart + preloadCount - 1);

        for (
            int index = preloadStart;
            index <= preloadEnd;
            index++)
        {
            if (FileList.Items[index]
                is IoFileListItem item)
            {
                _ = thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Preload);
            }
        }
    }

    private void FolderWatcher_FolderChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.InvokeAsync(
            ScheduleFolderRefresh);
    }

    private void ScheduleFolderRefresh()
    {
        folderRefreshCancellation?.Cancel();

        folderRefreshCancellation?.Dispose();

        folderRefreshCancellation =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            folderRefreshCancellation.Token;

        _ = DebouncedFolderRefreshAsync(
            cancellationToken);
    }

    private async Task DebouncedFolderRefreshAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(300),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await RefreshCurrentFolderAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when a new file system event
            // resets the debounce timer.
        }
    }

    protected override void OnClosed(
        EventArgs e)
    {
        folderRefreshCancellation?.Cancel();
        folderRefreshCancellation?.Dispose();

        folderWatcher.Dispose();

        base.OnClosed(e);
    }

    private void FileList_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem
            is not IoFileListItem item)
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Could not open the file.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}