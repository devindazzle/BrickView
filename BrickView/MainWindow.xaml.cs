// -----------------------------------------------------------------------------
// MainWindow.xaml.cs
//
// Contains the main BrickView window controller and coordinates folder loading,
// file watching, sorting, searching, thumbnail loading, metadata loading,
// tag management and user interactions.
//
// Model identity resolution is performed asynchronously so Windows file-system
// identity lookups do not block the UI while large folders are being loaded.
// Rename detection uses the stable model identity so the existing
// IoFileListItem can be retained when a file is renamed.
//
// Tag data is provided by one long-lived TagService instance. Tags are loaded
// into an IoFileListItem only after its stable ModelIdentity has been resolved.
// Tag lookups use the in-memory tag store and therefore do not perform disk I/O.
//
// TagPickerControl owns the tag-picker UI. MainWindow only supplies the target
// model and the shared TagService to that control.
// -----------------------------------------------------------------------------

using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BrickView;

public partial class MainWindow : Window {
    public static readonly DependencyProperty ThumbnailWidthProperty =
        DependencyProperty.Register(
            nameof(ThumbnailWidth),
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(
                ThumbnailSizes.Medium.ThumbnailWidth));

    public static readonly DependencyProperty ThumbnailHeightProperty =
        DependencyProperty.Register(
            nameof(ThumbnailHeight),
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(
                ThumbnailSizes.Medium.ThumbnailHeight));

    public static readonly DependencyProperty CardWidthProperty =
        DependencyProperty.Register(
            nameof(CardWidth),
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(
                ThumbnailSizes.Medium.CardWidth));

    public static readonly DependencyProperty CardHeightProperty =
        DependencyProperty.Register(
            nameof(CardHeight),
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(
                ThumbnailSizes.Medium.CardHeight));

    private readonly ThumbnailLoader thumbnailLoader;

    private readonly FolderDiffService folderDiffService;

    private readonly IoFolderWatcher folderWatcher;

    private readonly ThumbnailSizeManager thumbnailSizeManager;

    private readonly MetadataLoader metadataLoader;

    private readonly WindowStateService windowStateService;

    private readonly WindowsFileIdentityProvider fileIdentityProvider;

    private readonly TagService tagService;

    private readonly HashSet<IoFileListItem> metadataLoadingItems;

    private readonly List<IoFileListItem> allFileItems;

    private string? currentFolder;

    private string? restoredFolder;

    private CancellationTokenSource? folderRefreshCancellation;

    private CancellationTokenSource? modelIdentityCancellation;

    private SortField currentSortField =
        SortField.FileName;

    private FileSortDirection currentSortDirection =
        FileSortDirection.Ascending;

    private enum SortField {
        FileName,
        CreatedDate,
        ModifiedDate
    }

    public MainWindow() {
        InitializeComponent();

        Loaded +=
            MainWindow_Loaded;

        ApplicationStateService applicationStateService =
            new ApplicationStateService();

        windowStateService =
            new WindowStateService(
                applicationStateService);

        ApplicationState? restoredState =
            windowStateService.Restore(
                this);

        restoredFolder =
            restoredState?.LastSelectedFolder;

        if (restoredState is not null) {
            currentSortField =
                restoredState.SortField switch {
                    FileSortField.FileName =>
                        SortField.FileName,

                    FileSortField.CreatedDate =>
                        SortField.CreatedDate,

                    FileSortField.ModifiedDate =>
                        SortField.ModifiedDate,

                    _ =>
                        SortField.FileName
                };

            currentSortDirection =
                restoredState.SortDirection;
        }

        thumbnailSizeManager =
            ThumbnailSizeManager.Instance;

        thumbnailSizeManager.SizeChanged +=
            ThumbnailSizeManager_SizeChanged;

        metadataLoadingItems =
            new HashSet<IoFileListItem>();

        allFileItems =
            new List<IoFileListItem>();

        if (restoredState?.ThumbnailSizePreset is
            ThumbnailSizePreset restoredPreset) {
            thumbnailSizeManager.SetSize(
                restoredPreset);
        }

        ApplyThumbnailSize(
            thumbnailSizeManager.Current);

        SetThumbnailSizeSelector(
            thumbnailSizeManager.Current.Preset);

        thumbnailLoader =
            new ThumbnailLoader(360);

        // The identity provider is shared by both the initial model identity
        // loading and the folder diff service used for rename detection.
        fileIdentityProvider =
            new WindowsFileIdentityProvider();

        folderDiffService =
            new FolderDiffService(
                fileIdentityProvider);

        TagPersistenceService tagPersistenceService =
            new TagPersistenceService();

        // Keep one TagService instance for the lifetime of the window so all
        // cards use the same in-memory tag catalog and model-tag relationships.
        tagService =
            new TagService(
                tagPersistenceService);

        // The picker is shared by all model cards. It receives the same
        // TagService instance used by MainWindow.
        TagPicker.TagService =
            tagService;

        folderWatcher =
            new IoFolderWatcher();

        metadataLoader =
            new MetadataLoader();

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

        UpdateSortMenu();
    }

    public double ThumbnailWidth {
        get {
            return (double)GetValue(
                ThumbnailWidthProperty);
        }

        private set {
            SetValue(
                ThumbnailWidthProperty,
                value);
        }
    }

    public double ThumbnailHeight {
        get {
            return (double)GetValue(
                ThumbnailHeightProperty);
        }

        private set {
            SetValue(
                ThumbnailHeightProperty,
                value);
        }
    }

    public double CardWidth {
        get {
            return (double)GetValue(
                CardWidthProperty);
        }

        private set {
            SetValue(
                CardWidthProperty,
                value);
        }
    }

    public double CardHeight {
        get {
            return (double)GetValue(
                CardHeightProperty);
        }

        private set {
            SetValue(
                CardHeightProperty,
                value);
        }
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e) {
        Loaded -=
            MainWindow_Loaded;

        if (string.IsNullOrWhiteSpace(
                restoredFolder)) {
            ModelCountText.Text =
                "0 models";

            return;
        }

        string folder =
            restoredFolder;

        restoredFolder =
            null;

        if (!Directory.Exists(
                folder)) {
            currentFolder =
                null;

            NoFolderSelectedText.Visibility =
                Visibility.Visible;

            FolderText.Text =
                string.Empty;

            ModelCountText.Text =
                "0 models";

            return;
        }

        currentFolder =
            folder;

        NoFolderSelectedText.Visibility =
            Visibility.Collapsed;

        FolderText.Text =
            folder;

        folderWatcher.Start(
            folder);

        await LoadIoFilesAsync(
            folder);
    }

    private void ThumbnailSizeManager_SizeChanged(
        ThumbnailSizeDefinition newSize) {
        ApplyThumbnailSize(
            newSize);

        foreach (IoFileListItem item
                 in allFileItems) {
            item.InvalidateThumbnail();
        }

        FileList.InvalidateMeasure();
    }

    private void ApplyThumbnailSize(
        ThumbnailSizeDefinition size) {
        ThumbnailWidth =
            size.ThumbnailWidth;

        ThumbnailHeight =
            size.ThumbnailHeight;

        CardWidth =
            size.CardWidth;

        CardHeight =
            size.CardHeight;
    }

    private void SetThumbnailSizeSelector(
        ThumbnailSizePreset preset) {
        SmallThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Small;

        MediumThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Medium;

        LargeThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Large;
    }

    private void ThumbnailSizeSelector_Checked(
        object sender,
        RoutedEventArgs e) {
        if (sender is not System.Windows.Controls.RadioButton radioButton) {
            return;
        }

        if (radioButton.Tag is not string presetName) {
            return;
        }

        ThumbnailSizePreset preset;

        switch (presetName) {
            case "Small":

                preset =
                    ThumbnailSizePreset.Small;

                break;

            case "Medium":

                preset =
                    ThumbnailSizePreset.Medium;

                break;

            case "Large":

                preset =
                    ThumbnailSizePreset.Large;

                break;

            default:
                return;
        }

        if (thumbnailSizeManager.Current.Preset ==
            preset) {
            return;
        }

        thumbnailSizeManager.SetSize(
            preset);
    }

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

    private async void RefreshView_Click(
        object sender,
        RoutedEventArgs e) {
        await RefreshCurrentFolderAsync();
    }

    private async Task LoadIoFilesAsync(
        string folder) {
        // A new folder load invalidates outstanding identity lookups for the
        // previous folder. Existing background operations therefore cannot
        // update items that no longer belong to the current view.
        modelIdentityCancellation?.Cancel();

        modelIdentityCancellation?.Dispose();

        modelIdentityCancellation =
            new CancellationTokenSource();

        allFileItems.Clear();

        metadataLoadingItems.Clear();

        FileList.Items.Clear();

        NoResultsText.Visibility =
            Visibility.Collapsed;

        ModelCountText.Text =
            "0 models";

        string[] files =
            Directory.GetFiles(
                folder,
                "*.io",
                SearchOption.TopDirectoryOnly)
                .OrderBy(
                    file => Path.GetFileName(file),
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        foreach (string file in files) {
            AddFileListItem(
                file);
        }

        SortAllFileItems();

        ApplySearchFilter();

        await Task.CompletedTask;
    }

    private async Task RefreshCurrentFolderAsync() {
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

            metadataLoadingItems.Clear();

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
            await folderDiffService.CompareAsync(
                existingItems,
                currentFiles);

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

            // The item may have been removed while the background operation
            // was running. Do not attach the result to an item that is no
            // longer part of the current folder view.
            if (!allFileItems.Contains(item)) {
                return;
            }

            item.SetModelIdentity(
                modelIdentity);

            // TagService keeps tag data in memory, so loading the tags for a
            // model does not perform disk I/O and does not need its own
            // asynchronous operation. The lookup only runs after the stable
            // model identity has been resolved.
            item.SetTags(
                tagService.GetTags(
                    modelIdentity));
        }
        catch (OperationCanceledException) {
            // Expected when a folder reload or application shutdown cancels
            // an outstanding identity lookup.
        }
    }

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

        // Keep the existing IoFileListItem so any tag data associated with the
        // model remains attached to the same object.
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
            metadataLoadingItems.Remove(
                item);

            allFileItems.Remove(
                item);
        }
    }

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

    private void AddTagButton_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Button button) {
            return;
        }

        if (button.DataContext
            is not IoFileListItem item) {
            return;
        }

        // The shared picker is positioned relative to the specific plus
        // button that the user clicked.
        TagPicker.OpenFor(
            button,
            item);

        e.Handled = true;
    }

    private void RemoveTag_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Button button) {
            return;
        }

        if (button.DataContext
            is not TagDefinition tag) {
            return;
        }

        if (button.Tag is not IoFileListItem item) {
            return;
        }

        if (item.ModelIdentity is null) {
            return;
        }

        bool removed =
            tagService.RemoveTag(
                item.ModelIdentity,
                tag.Name);

        if (removed) {
            item.SetTags(
                tagService.GetTags(
                    item.ModelIdentity));
        }

        e.Handled = true;
    }

    private void SortFileName_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            SortField.FileName);
    }

    private void SortCreatedDate_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            SortField.CreatedDate);
    }

    private void SortModifiedDate_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            SortField.ModifiedDate);
    }

    private void SortAscending_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortDirection(
            FileSortDirection.Ascending);
    }

    private void SortDescending_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortDirection(
            FileSortDirection.Descending);
    }

    private void SetSortField(
        SortField sortField) {
        if (currentSortField ==
            sortField) {
            return;
        }

        currentSortField =
            sortField;

        SortAllFileItems();

        ApplySearchFilter();

        UpdateSortMenu();
    }

    private void SetSortDirection(
        FileSortDirection sortDirection) {
        if (currentSortDirection ==
            sortDirection) {
            return;
        }

        currentSortDirection =
            sortDirection;

        SortAllFileItems();

        ApplySearchFilter();

        UpdateSortMenu();
    }

    private void SortAllFileItems() {
        switch (currentSortField) {
            case SortField.FileName:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        StringComparer.OrdinalIgnoreCase.Compare(
                            left.FileName,
                            right.FileName));

                break;

            case SortField.CreatedDate:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        DateTime.Compare(
                            left.CreationTimeUtc,
                            right.CreationTimeUtc));

                break;

            case SortField.ModifiedDate:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        DateTime.Compare(
                            left.LastWriteTimeUtc,
                            right.LastWriteTimeUtc));

                break;
        }

        if (currentSortDirection ==
            FileSortDirection.Descending) {
            allFileItems.Reverse();
        }
    }

    private void UpdateSortMenu() {
        FileNameSortCheck.Visibility =
            currentSortField ==
            SortField.FileName
                ? Visibility.Visible
                : Visibility.Collapsed;

        CreatedDateSortCheck.Visibility =
            currentSortField ==
            SortField.CreatedDate
                ? Visibility.Visible
                : Visibility.Collapsed;

        ModifiedDateSortCheck.Visibility =
            currentSortField ==
            SortField.ModifiedDate
                ? Visibility.Visible
                : Visibility.Collapsed;

        AscendingSortCheck.Visibility =
            currentSortDirection ==
            FileSortDirection.Ascending
                ? Visibility.Visible
                : Visibility.Collapsed;

        DescendingSortCheck.Visibility =
            currentSortDirection ==
            FileSortDirection.Descending
                ? Visibility.Visible
                : Visibility.Collapsed;

        SortDirectionText.Text =
            currentSortDirection ==
            FileSortDirection.Ascending
                ? "↑"
                : "↓";

        switch (currentSortField) {
            case SortField.FileName:

                SortButtonContent.Text =
                    "File name";

                break;

            case SortField.CreatedDate:

                SortButtonContent.Text =
                    "Created date";

                break;

            case SortField.ModifiedDate:

                SortButtonContent.Text =
                    "Modified date";

                break;
        }
    }

    private void ApplySearchFilter() {
        string searchText =
            SearchTextBox.Text.Trim();

        IEnumerable<IoFileListItem> filteredItems =
            allFileItems;

        if (!string.IsNullOrEmpty(
                searchText)) {

            if (!searchText.Contains(
                    '*',
                    StringComparison.Ordinal)) {

                filteredItems =
                    allFileItems.Where(
                        item =>
                            $"{item.FileName}.io".Contains(
                                searchText,
                                StringComparison.OrdinalIgnoreCase));
            }
            else {

                string[] searchParts =
                    searchText
                        .Split('*')
                        .Where(
                            part =>
                                !string.IsNullOrEmpty(part))
                        .ToArray();

                filteredItems =
                    allFileItems.Where(
                        item => {
                            if (searchParts.Length == 0) {
                                return true;
                            }

                            string fileName =
                                $"{item.FileName}.io";

                            int searchPosition =
                                0;

                            foreach (string searchPart
                                     in searchParts) {
                                int matchPosition =
                                    fileName.IndexOf(
                                        searchPart,
                                        searchPosition,
                                        StringComparison.OrdinalIgnoreCase);

                                if (matchPosition < 0) {
                                    return false;
                                }

                                searchPosition =
                                    matchPosition +
                                    searchPart.Length;
                            }

                            return true;
                        });
            }
        }

        List<IoFileListItem> visibleItems =
            filteredItems.ToList();

        FileList.Items.Clear();

        foreach (IoFileListItem item
                 in visibleItems) {
            FileList.Items.Add(
                item);
        }

        int visibleCount =
            visibleItems.Count;

        int totalCount =
            allFileItems.Count;

        if (visibleCount == totalCount) {
            ModelCountText.Text =
                CreateModelCountText(
                    totalCount);
        }
        else {
            ModelCountText.Text =
                $"{visibleCount} of {totalCount} models";
        }

        if (!string.IsNullOrEmpty(searchText)
            && visibleItems.Count == 0) {
            NoResultsText.Visibility =
                Visibility.Visible;
        }
        else {
            NoResultsText.Visibility =
                Visibility.Collapsed;
        }
    }

    private string CreateModelCountText(
        int modelCount) {
        return modelCount == 1
            ? "1 model"
            : $"{modelCount} models";
    }

    private void SearchTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e) {
        ApplySearchFilter();
    }

    private void SearchTextBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e) {
        if (e.Key != Key.Escape) {
            return;
        }

        SearchTextBox.Clear();

        SearchTextBox.Focus();

        e.Handled = true;
    }

    private void FileList_ViewportChanged(
        object sender,
        RoutedEventArgs e) {
        if (e is not ViewportChangedEventArgs
            viewportEventArgs) {
            return;
        }

        int firstVisibleIndex =
            viewportEventArgs.FirstVisibleIndex;

        int lastVisibleIndex =
            viewportEventArgs.LastVisibleIndex;

        int itemCount =
            FileList.Items.Count;

        if (itemCount == 0) {
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
            index++) {
            if (FileList.Items[index]
                is IoFileListItem item) {

                _ = thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Visible);

                _ = LoadMetadataAsync(
                    item);
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
            index++) {
            if (FileList.Items[index]
                is IoFileListItem item) {

                _ = thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Preload);
            }
        }
    }

    private async Task LoadMetadataAsync(
        IoFileListItem item) {
        if (item.Metadata is not null) {
            return;
        }

        if (!metadataLoadingItems.Add(
                item)) {
            return;
        }

        try {
            long fileSize =
                item.FileSize;

            DateTime lastWriteTimeUtc =
                item.LastWriteTimeUtc;

            IoModelMetadata? metadata =
                await metadataLoader.LoadAsync(
                    item.FilePath);

            if (item.FileSize != fileSize ||
                item.LastWriteTimeUtc != lastWriteTimeUtc) {
                return;
            }

            item.Metadata =
                metadata;
        }
        finally {
            metadataLoadingItems.Remove(
                item);
        }
    }

    private void Thumbnail_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e) {
        if (sender is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        OpenFile(
            item);

        e.Handled = true;
    }

    private void OpenInStudio_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        OpenFile(
            item);
    }

    private void OpenFile(
        IoFileListItem item) {
        if (item.HasError) {
            return;
        }

        try {
            Process.Start(
                new ProcessStartInfo {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not open the file.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ShowInFileExplorer_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        if (!File.Exists(
                item.FilePath)) {
            MessageBox.Show(
                "The file no longer exists.",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        try {
            Process.Start(
                new ProcessStartInfo {
                    FileName = "explorer.exe",
                    Arguments =
                        $"/select,\"{item.FilePath}\"",
                    UseShellExecute = true
                });
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not open File Explorer.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CopyFilePath_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        try {
            Clipboard.SetText(
                item.FilePath);
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not copy the file path.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CopyFileName_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        try {
            Clipboard.SetText(
                Path.GetFileName(
                    item.FilePath));
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not copy the file name.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void FolderWatcher_FolderChanged(
        object? sender,
        EventArgs e) {
        Dispatcher.InvokeAsync(
            ScheduleFolderRefresh);
    }

    private void ScheduleFolderRefresh() {
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
        CancellationToken cancellationToken) {
        try {
            await Task.Delay(
                TimeSpan.FromMilliseconds(300),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            await RefreshCurrentFolderAsync();
        }
        catch (OperationCanceledException) {
            // Expected when a new file system event resets
            // the debounce timer.
        }
    }

    private FileSortField GetFileSortField() {
        switch (currentSortField) {
            case SortField.FileName:

                return FileSortField.FileName;

            case SortField.CreatedDate:

                return FileSortField.CreatedDate;

            case SortField.ModifiedDate:

                return FileSortField.ModifiedDate;

            default:

                return FileSortField.FileName;
        }
    }

    protected override void OnClosed(
        EventArgs e) {
        windowStateService.Save(
            this,
            currentFolder,
            thumbnailSizeManager.Current.Preset,
            GetFileSortField(),
            currentSortDirection);

        folderRefreshCancellation?.Cancel();

        folderRefreshCancellation?.Dispose();

        modelIdentityCancellation?.Cancel();

        modelIdentityCancellation?.Dispose();

        thumbnailSizeManager.SizeChanged -=
            ThumbnailSizeManager_SizeChanged;

        folderWatcher.Dispose();

        base.OnClosed(e);
    }
}