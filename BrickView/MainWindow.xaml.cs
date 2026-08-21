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

using System.IO;
using System.Windows;

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
            new ThumbnailLoader();

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

        modelIdentityCancellation?.Cancel();

        modelIdentityCancellation?.Dispose();

        thumbnailSizeManager.SizeChanged -=
            ThumbnailSizeManager_SizeChanged;

        folderWatcher.Dispose();

        base.OnClosed(e);
    }
}