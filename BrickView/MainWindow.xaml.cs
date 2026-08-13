using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace BrickView;

public partial class MainWindow : Window
{
    private readonly ThumbnailLoader thumbnailLoader;

    private readonly FolderDiffService folderDiffService;

    private readonly IoFolderWatcher folderWatcher;

    private readonly List<IoFileListItem> allFileItems;

    private string? currentFolder;

    private CancellationTokenSource? folderRefreshCancellation;

    public MainWindow()
    {
        InitializeComponent();

        thumbnailLoader =
            new ThumbnailLoader(360);

        folderDiffService =
            new FolderDiffService();

        folderWatcher =
            new IoFolderWatcher();

        allFileItems =
            new List<IoFileListItem>();

        folderWatcher.FolderChanged +=
            FolderWatcher_FolderChanged;

        FileList.AddHandler(
            VirtualizingWrapPanel.ViewportChangedEvent,
            new RoutedEventHandler(
                FileList_ViewportChanged));

        SearchTextBox.TextChanged +=
            SearchTextBox_TextChanged;

        SearchTextBox.PreviewKeyDown +=
            SearchTextBox_PreviewKeyDown;
    }

    private async void SelectFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenFolderDialog dialog =
            new OpenFolderDialog
            {
                Title = "Pick Folder"
            };

        bool? result =
            dialog.ShowDialog();

        if (result != true)
        {
            return;
        }

        string folder =
            dialog.FolderName;

        if (string.IsNullOrWhiteSpace(folder))
        {
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

    private async void RefreshView_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshCurrentFolderAsync();
    }

    private async Task LoadIoFilesAsync(
        string folder)
    {
        allFileItems.Clear();

        FileList.Items.Clear();

        NoResultsText.Visibility =
            Visibility.Collapsed;

        string[] files =
            Directory.GetFiles(
                folder,
                "*.io",
                SearchOption.TopDirectoryOnly)
                .OrderBy(
                    file => Path.GetFileName(file),
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        foreach (string file in files)
        {
            AddFileListItem(
                file);
        }

        SortAllFileItems();

        ApplySearchFilter();

        await Task.CompletedTask;
    }

    private async Task RefreshCurrentFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(
                currentFolder))
        {
            return;
        }

        if (!Directory.Exists(
                currentFolder))
        {
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

            return;
        }

        NoFolderSelectedText.Visibility =
            Visibility.Collapsed;

        string[] currentFiles =
            Directory.GetFiles(
                currentFolder,
                "*.io",
                SearchOption.TopDirectoryOnly)
                .OrderBy(
                    file => Path.GetFileName(file),
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        List<IoFileListItem> existingItems =
            allFileItems.ToList();

        FolderDiff diff =
            folderDiffService.Compare(
                existingItems,
                currentFiles);

        foreach (FileChange change
                 in diff.Changes)
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

        SortAllFileItems();

        ApplySearchFilter();

        await Task.CompletedTask;
    }

    private void AddFileListItem(
        string filePath)
    {
        if (!File.Exists(
                filePath))
        {
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
                fileInfo.LastWriteTimeUtc,
                null,
                null);

        allFileItems.Add(
            item);
    }

    private void RemoveFileListItem(
        string filePath)
    {
        IoFileListItem? item =
            allFileItems
                .FirstOrDefault(
                    existingItem =>
                        string.Equals(
                            existingItem.FilePath,
                            filePath,
                            StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            allFileItems.Remove(
                item);
        }
    }

    private void UpdateModifiedFile(
        string filePath)
    {
        IoFileListItem? item =
            allFileItems
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

        if (!File.Exists(
                filePath))
        {
            return;
        }

        FileInfo fileInfo =
            new FileInfo(
                filePath);

        item.UpdateFileInfo(
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc);

        item.InvalidateThumbnail();
    }

    private void SortAllFileItems()
    {
        allFileItems.Sort(
            (
                left,
                right) =>
                StringComparer.OrdinalIgnoreCase.Compare(
                    left.FileName,
                    right.FileName));
    }

    private void ApplySearchFilter()
    {
        string searchText =
            SearchTextBox.Text.Trim();

        IEnumerable<IoFileListItem> filteredItems =
            allFileItems;

        if (!string.IsNullOrEmpty(
                searchText))
        {
            filteredItems =
                allFileItems.Where(
                    item =>
                        item.FileName.Contains(
                            searchText,
                            StringComparison.OrdinalIgnoreCase));
        }

        List<IoFileListItem> visibleItems =
            filteredItems.ToList();

        FileList.Items.Clear();

        foreach (IoFileListItem item
                 in visibleItems)
        {
            FileList.Items.Add(
                item);
        }

        if (!string.IsNullOrEmpty(searchText)
            && visibleItems.Count == 0)
        {
            NoResultsText.Visibility =
                Visibility.Visible;
        }
        else
        {
            NoResultsText.Visibility =
                Visibility.Collapsed;
        }
    }

    private void SearchTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplySearchFilter();
    }

    private void SearchTextBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        SearchTextBox.Clear();

        SearchTextBox.Focus();

        e.Handled = true;
    }

    private void FileList_ViewportChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (e is not ViewportChangedEventArgs
            viewportEventArgs)
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
                preloadStart +
                preloadCount -
                1);

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

    private void Thumbnail_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item)
        {
            return;
        }

        if (item.HasError)
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

            e.Handled = true;
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
}