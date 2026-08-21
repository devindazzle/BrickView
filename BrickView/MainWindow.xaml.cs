// -----------------------------------------------------------------------------
// MainWindow.xaml.cs
//
// Defines the main BrickView window controller and coordinates the interaction
// between the window UI and the services responsible for folder loading,
// file-system monitoring, sorting, searching, thumbnail loading, metadata,
// tags, model identity and application state.
//
// MainWindow owns the presentation-level orchestration for the model browser.
// It does not implement the underlying services; instead, it delegates
// specialized work to the corresponding service classes.
//
// Model identity resolution is performed asynchronously so Windows file-system
// identity lookups do not block the UI while large folders are being loaded.
// Rename detection uses the stable model identity so the existing
// IoFileListItem can be retained when a file is renamed.
//
// Tag data is provided by one long-lived TagService instance. Tags are loaded
// into an IoFileListItem only after its stable ModelIdentity has been resolved.
// TagPickerControl owns the tag-picker UI; MainWindow supplies the target model
// and the shared TagService instance.
//
// Search interpretation is handled by SmartSearchQuery and SmartSearchEngine.
// MainWindow coordinates the search UI and applies the resulting filter.
//
// No diagnostic or temporary debug code belongs in this production controller.
// -----------------------------------------------------------------------------

using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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

    private readonly SmartSearchEngine smartSearchEngine;

    private readonly List<IoFileListItem> allFileItems;

    private string? currentFolder;

    private string? restoredFolder;

    private CancellationTokenSource? folderRefreshCancellation;

    private CancellationTokenSource? modelIdentityCancellation;

    private bool favoriteFilterEnabled;

    private SmartSearchQuery currentSearchQuery =
        SmartSearchQuery.Empty;

    private bool searchRefreshPending;

    private SortField currentSortField =
        SortField.FileName;

    private FileSortDirection currentSortDirection =
        FileSortDirection.Ascending;

    private enum SortField {
        FileName,
        CreatedDate,
        ModifiedDate
    }

    /// <summary>
    /// Initializes the main BrickView window, restores persisted window state,
    /// creates the required services and connects UI and file-system events.
    /// </summary>
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

        smartSearchEngine =
            new SmartSearchEngine();

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

        FavoriteFilterButton.ToolTip =
            "Show favorites only";
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

    /// <summary>
    /// Completes startup after the window has loaded and restores the previously
    /// selected folder when that folder still exists.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="e">Event data supplied by WPF.</param>
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

    /// <summary>
    /// Applies a newly selected thumbnail size and invalidates existing
    /// thumbnails so they can be regenerated at the new dimensions.
    /// </summary>
    /// <param name="newSize">The thumbnail-size definition to apply.</param>
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

    /// <summary>
    /// Updates the dependency properties that determine thumbnail and card dimensions.
    /// </summary>
    /// <param name="size">The thumbnail-size definition to apply.</param>
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

    /// <summary>
    /// Synchronizes the thumbnail-size radio buttons with the selected preset.
    /// </summary>
    /// <param name="preset">The currently selected thumbnail-size preset.</param>
    private void SetThumbnailSizeSelector(
        ThumbnailSizePreset preset) {
        SmallThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Small;

        MediumThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Medium;

        LargeThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Large;
    }

    /// <summary>
    /// Handles a thumbnail-size selection and delegates the actual size change
    /// to the shared thumbnail-size manager.
    /// </summary>
    /// <param name="sender">The radio button that raised the event.</param>
    /// <param name="e">Event data supplied by WPF.</param>
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
        await RefreshCurrentFolderAsync();
    }

    /// <summary>
    /// Loads all top-level .io files from the specified folder into the model list,
    /// starts asynchronous identity resolution and applies the current sort and filter.
    /// </summary>
    /// <param name="folder">The folder containing the BrickView model files.</param>
    /// <returns>A completed task after the initial file list has been prepared.</returns>
    private Task LoadIoFilesAsync(
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

        return Task.CompletedTask;
    }

    /// <summary>
    /// Compares the current folder with the existing model list and applies additions,
    /// removals, modifications and renames before refreshing the visible list.
    /// </summary>
    /// <returns>A task that completes when the folder comparison and UI refresh finish.</returns>
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
    /// Toggles the Favorite state of the model represented by the clicked indicator
    /// and refreshes the list when the Favorite filter is active.
    /// </summary>
    /// <param name="sender">The Favorite indicator that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
    private void FavoriteIndicator_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e) {
        if (sender is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        if (item.ModelIdentity is null) {
            return;
        }

        bool newFavoriteState =
            !item.IsFavorite;

        bool changed =
            tagService.SetFavorite(
                item.ModelIdentity,
                newFavoriteState);

        if (changed) {
            item.IsFavorite =
                newFavoriteState;
        }

        e.Handled = true;

        if (favoriteFilterEnabled) {
            ApplySearchFilter();
        }
    }

    /// <summary>
    /// Opens the shared tag picker for the model represented by the clicked Add Tag button.
    /// </summary>
    /// <param name="sender">The Add Tag button that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
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

        TagPicker.OpenFor(
            button,
            item);

        e.Handled = true;
    }

    /// <summary>
    /// Removes the selected tag from the model represented by the clicked tag button
    /// and refreshes the active search when necessary.
    /// </summary>
    /// <param name="sender">The tag remove button that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
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

            if (!currentSearchQuery.IsEmpty) {
                RequestSearchRefresh();
            }
        }

        e.Handled = true;
    }

    /// <summary>
    /// Selects file name as the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortFileName_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            SortField.FileName);
    }

    /// <summary>
    /// Selects creation date as the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortCreatedDate_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            SortField.CreatedDate);
    }

    /// <summary>
    /// Selects last-modified date as the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortModifiedDate_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            SortField.ModifiedDate);
    }

    /// <summary>
    /// Selects ascending order for the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortAscending_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortDirection(
            FileSortDirection.Ascending);
    }

    /// <summary>
    /// Selects descending order for the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortDescending_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortDirection(
            FileSortDirection.Descending);
    }

    /// <summary>
    /// Changes the active sort field and reapplies sorting, filtering and menu state.
    /// </summary>
    /// <param name="sortField">The field to use for sorting.</param>
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

    /// <summary>
    /// Changes the active sort direction and reapplies sorting, filtering and menu state.
    /// </summary>
    /// <param name="sortDirection">The direction to use for sorting.</param>
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

    /// <summary>
    /// Sorts the complete in-memory model list using the selected field and direction.
    /// </summary>
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

    /// <summary>
    /// Updates the sort popup controls and toolbar labels to reflect the current sort state.
    /// </summary>
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

    /// <summary>
    /// Applies the Favorite filter and Smart Search query to the complete model list
    /// and updates the visible ListBox contents and result count.
    /// </summary>
    private void ApplySearchFilter() {
        SmartSearchQuery query =
            currentSearchQuery;

        IEnumerable<IoFileListItem> filteredItems =
            allFileItems;

        if (favoriteFilterEnabled) {
            filteredItems =
                filteredItems.Where(
                    item =>
                        item.IsFavorite);
        }

        filteredItems =
            smartSearchEngine.Search(
                filteredItems,
                query);

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

        bool hasActiveFilter =
            favoriteFilterEnabled ||
            !query.IsEmpty;

        NoResultsText.Visibility =
            hasActiveFilter &&
            visibleItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the Favorite-only filter state from the toolbar button and reapplies the filter.
    /// </summary>
    /// <param name="sender">The Favorite filter button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void FavoriteFilterButton_Click(
        object sender,
        RoutedEventArgs e) {
        favoriteFilterEnabled =
            FavoriteFilterButton.IsChecked == true;

        FavoriteFilterButton.ToolTip =
            favoriteFilterEnabled
                ? "Show all models"
                : "Show favorites only";

        ApplySearchFilter();
    }

    /// <summary>
    /// Determines whether Favorite filtering or a Smart Search query is currently active.
    /// </summary>
    /// <returns><see langword="true"/> when at least one search filter is active.</returns>
    private bool HasActiveSearchFilter() {
        return favoriteFilterEnabled ||
               !currentSearchQuery.IsEmpty;
    }

    /// <summary>
    /// Schedules a deferred search refresh when identity-dependent search state changes,
    /// coalescing multiple requests into a single UI update.
    /// </summary>
    private void RequestSearchRefresh() {
        if (!HasActiveSearchFilter() ||
            searchRefreshPending) {
            return;
        }

        searchRefreshPending =
            true;

        Dispatcher.BeginInvoke(
            new Action(
                () => {
                    searchRefreshPending =
                        false;

                    ApplySearchFilter();
                }),
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Creates the singular or plural result-count text used by the model browser.
    /// </summary>
    /// <param name="modelCount">The number of models.</param>
    /// <returns>A correctly pluralized model-count string.</returns>
    private string CreateModelCountText(
        int modelCount) {
        return modelCount == 1
            ? "1 model"
            : $"{modelCount} models";
    }

    /// <summary>
    /// Parses the current Smart Search text and applies the resulting query immediately.
    /// </summary>
    /// <param name="sender">The search text box that changed.</param>
    /// <param name="e">Text-change event data supplied by WPF.</param>
    private void SearchTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e) {
        currentSearchQuery =
            SmartSearchQuery.Parse(
                SearchTextBox.Text);

        ApplySearchFilter();
    }

    /// <summary>
    /// Clears the Smart Search field when the user presses Escape.
    /// </summary>
    /// <param name="sender">The search text box receiving the key event.</param>
    /// <param name="e">Keyboard event data supplied by WPF.</param>
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

    /// <summary>
    /// Loads thumbnails for visible models and preloads thumbnails immediately beyond
    /// the visible viewport to improve scrolling responsiveness.
    /// </summary>
    /// <param name="sender">The virtualized file list that raised the event.</param>
    /// <param name="e">Viewport event data containing the visible item range.</param>
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

    /// <summary>
    /// Loads metadata for a model when it has not already been loaded and discards
    /// stale results if the underlying file changed while loading was in progress.
    /// </summary>
    /// <param name="item">The model-list item whose metadata should be loaded.</param>
    /// <returns>A task representing the asynchronous metadata load.</returns>
    private async Task LoadMetadataAsync(
        IoFileListItem item) {
        if (item.Metadata is not null) {
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
        catch (Exception) {
            // Metadata loading failures are intentionally ignored here.
            // The existing model remains usable without metadata.
        }
    }

    /// <summary>
    /// Opens the model file represented by the clicked thumbnail.
    /// </summary>
    /// <param name="sender">The thumbnail element that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
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

    /// <summary>
    /// Opens the model associated with the context menu in the default application.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
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

    /// <summary>
    /// Opens the specified model file using the Windows shell.
    /// </summary>
    /// <param name="item">The model-list item whose file should be opened.</param>
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

    /// <summary>
    /// Opens Windows File Explorer with the selected model file highlighted.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
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

    /// <summary>
    /// Copies the full path of the selected model file to the Windows clipboard.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
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

    /// <summary>
    /// Copies the file name of the selected model to the Windows clipboard.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
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
        folderRefreshCancellation?.Cancel();

        folderRefreshCancellation?.Dispose();

        folderRefreshCancellation =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            folderRefreshCancellation.Token;

        _ = DebouncedFolderRefreshAsync(
            cancellationToken);
    }

    /// <summary>
    /// Waits briefly after a file-system event before refreshing the current folder,
    /// allowing bursts of file-system notifications to be processed as one update.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel superseded refresh requests.</param>
    /// <returns>A task representing the debounced refresh operation.</returns>
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

    /// <summary>
    /// Converts the window's internal sort-field representation to the persisted
    /// application-state representation.
    /// </summary>
    /// <returns>The corresponding persisted file-sort field.</returns>
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

    /// <summary>
    /// Persists the current window state and releases event subscriptions, watchers
    /// and cancellation resources when the main window closes.
    /// </summary>
    /// <param name="e">Event data supplied by WPF.</param>
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