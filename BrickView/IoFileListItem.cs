// -----------------------------------------------------------------------------
// IoFileListItem.cs
//
// Represents one .io model file displayed by BrickView.
//
// The class contains the file information used by the UI together with
// thumbnail, metadata, tag and favorite state.
//
// A stable ModelIdentity is associated with the item once it has been resolved.
// This identity is independent of the current file path, allowing model data
// such as tags and favorites to remain associated with a model when its file
// is renamed.
//
// Windows-specific identity resolution and persistence are intentionally kept
// outside this class. IoFileListItem only stores the resolved model identity
// and the data supplied by the relevant services.
// -----------------------------------------------------------------------------

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace BrickView;

/// <summary>
/// Represents a single BrickLink Studio .io model in the BrickView model list.
/// </summary>
public class IoFileListItem : INotifyPropertyChanged {
    /// <summary>
    /// Gets the file name without its extension.
    /// </summary>
    public string FileName {
        get;
        private set;
    }

    /// <summary>
    /// Gets the complete path to the model file.
    /// </summary>
    public string FilePath {
        get;
        private set;
    }

    /// <summary>
    /// Gets the current file size in bytes.
    /// </summary>
    public long FileSize {
        get;
        private set;
    }

    /// <summary>
    /// Gets the file creation time in UTC.
    /// </summary>
    public DateTime CreationTimeUtc {
        get;
        private set;
    }

    /// <summary>
    /// Gets the last file modification time in UTC.
    /// </summary>
    public DateTime LastWriteTimeUtc {
        get;
        private set;
    }

    /// <summary>
    /// Gets the stable identity of the model, independent of its current path.
    /// </summary>
    public ModelIdentity? ModelIdentity {
        get;
        private set;
    }

    private IReadOnlyList<TagDefinition> tags;

    /// <summary>
    /// Gets the current tags assigned to the model.
    ///
    /// The collection is a read-only snapshot supplied by TagService.
    /// </summary>
    public IReadOnlyList<TagDefinition> Tags {
        get {
            return tags;
        }
    }

    private bool isFavorite;

    /// <summary>
    /// Gets or sets whether the model is marked as a favorite.
    /// </summary>
    public bool IsFavorite {
        get {
            return isFavorite;
        }

        set {
            if (isFavorite == value) {
                return;
            }

            isFavorite =
                value;

            OnPropertyChanged();
        }
    }

    private IoModelMetadata? metadata;

    /// <summary>
    /// Gets or sets the metadata loaded for the model.
    /// </summary>
    public IoModelMetadata? Metadata {
        get {
            return metadata;
        }

        set {
            if (metadata == value) {
                return;
            }

            metadata =
                value;

            OnPropertyChanged();
        }
    }

    private BitmapImage? thumbnail;

    /// <summary>
    /// Gets or sets the thumbnail currently displayed for the model.
    /// </summary>
    public BitmapImage? Thumbnail {
        get {
            return thumbnail;
        }

        set {
            if (thumbnail == value) {
                return;
            }

            thumbnail =
                value;

            OnPropertyChanged();
        }
    }

    private string? errorMessage;

    /// <summary>
    /// Gets or sets the error message associated with the model, if any.
    /// </summary>
    public string? ErrorMessage {
        get {
            return errorMessage;
        }

        set {
            if (errorMessage == value) {
                return;
            }

            errorMessage =
                value;

            OnPropertyChanged();

            OnPropertyChanged(
                nameof(HasError));
        }
    }

    /// <summary>
    /// Gets whether the model currently has an error.
    /// </summary>
    public bool HasError {
        get {
            return ErrorMessage is not null;
        }
    }

    private ThumbnailStatus thumbnailStatus;

    /// <summary>
    /// Gets or sets the current loading state of the thumbnail.
    /// </summary>
    public ThumbnailStatus ThumbnailStatus {
        get {
            return thumbnailStatus;
        }

        set {
            if (thumbnailStatus == value) {
                return;
            }

            thumbnailStatus =
                value;

            OnPropertyChanged();

            // These convenience properties are bound directly by the XAML.
            OnPropertyChanged(
                nameof(IsThumbnailNotLoaded));

            OnPropertyChanged(
                nameof(IsThumbnailLoading));

            OnPropertyChanged(
                nameof(IsThumbnailLoaded));

            OnPropertyChanged(
                nameof(IsThumbnailMissing));

            OnPropertyChanged(
                nameof(IsThumbnailError));
        }
    }

    /// <summary>
    /// Gets whether the thumbnail has not started loading.
    /// </summary>
    public bool IsThumbnailNotLoaded {
        get {
            return ThumbnailStatus ==
                   ThumbnailStatus.NotLoaded;
        }
    }

    /// <summary>
    /// Gets whether the thumbnail is currently loading.
    /// </summary>
    public bool IsThumbnailLoading {
        get {
            return ThumbnailStatus ==
                   ThumbnailStatus.Loading;
        }
    }

    /// <summary>
    /// Gets whether the thumbnail loaded successfully.
    /// </summary>
    public bool IsThumbnailLoaded {
        get {
            return ThumbnailStatus ==
                   ThumbnailStatus.Loaded;
        }
    }

    /// <summary>
    /// Gets whether no thumbnail is available for the model.
    /// </summary>
    public bool IsThumbnailMissing {
        get {
            return ThumbnailStatus ==
                   ThumbnailStatus.Missing;
        }
    }

    /// <summary>
    /// Gets whether thumbnail loading failed.
    /// </summary>
    public bool IsThumbnailError {
        get {
            return ThumbnailStatus ==
                   ThumbnailStatus.Error;
        }
    }

    /// <summary>
    /// Initializes a model item with its initial file information.
    /// </summary>
    /// <param name="fileName">
    /// The file name without its extension.
    /// </param>
    /// <param name="filePath">
    /// The complete path to the model file.
    /// </param>
    /// <param name="fileSize">
    /// The current file size in bytes.
    /// </param>
    /// <param name="creationTimeUtc">
    /// The file creation time in UTC.
    /// </param>
    /// <param name="lastWriteTimeUtc">
    /// The last modification time in UTC.
    /// </param>
    /// <param name="thumbnail">
    /// An initial thumbnail, if already available.
    /// </param>
    /// <param name="errorMessage">
    /// An initial error message, if one is known.
    /// </param>
    public IoFileListItem(
        string fileName,
        string filePath,
        long fileSize,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc,
        BitmapImage? thumbnail,
        string? errorMessage) {
        FileName =
            fileName;

        FilePath =
            filePath;

        FileSize =
            fileSize;

        CreationTimeUtc =
            creationTimeUtc;

        LastWriteTimeUtc =
            lastWriteTimeUtc;

        this.thumbnail =
            thumbnail;

        this.errorMessage =
            errorMessage;

        ModelIdentity =
            null;

        tags =
            Array.Empty<TagDefinition>();

        isFavorite =
            false;

        metadata =
            null;

        thumbnailStatus =
            ThumbnailStatus.NotLoaded;
    }

    /// <summary>
    /// Assigns the stable model identity once it has been resolved.
    /// </summary>
    /// <param name="modelIdentity">
    /// The stable identity of the model.
    /// </param>
    public void SetModelIdentity(
        ModelIdentity modelIdentity) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        if (ModelIdentity == modelIdentity) {
            return;
        }

        // The stable identity is kept separate from FilePath so model-related
        // data can survive a file rename.
        ModelIdentity =
            modelIdentity;

        OnPropertyChanged();
    }

    /// <summary>
    /// Replaces the current tag snapshot with the tags supplied by TagService.
    /// </summary>
    /// <param name="tags">
    /// The tags currently assigned to the model.
    /// </param>
    public void SetTags(
        IReadOnlyList<TagDefinition> tags) {
        ArgumentNullException.ThrowIfNull(
            tags);

        // Use a new array so bindings receive a new ItemsSource value whenever
        // the assigned tag collection changes.
        this.tags =
            tags.ToArray();

        OnPropertyChanged(
            nameof(Tags));
    }

    /// <summary>
    /// Updates the file path and associated file-system information after a
    /// rename or move.
    /// </summary>
    /// <param name="filePath">
    /// The model's new file path.
    /// </param>
    /// <param name="fileSize">
    /// The new file size in bytes.
    /// </param>
    /// <param name="creationTimeUtc">
    /// The file creation time in UTC.
    /// </param>
    /// <param name="lastWriteTimeUtc">
    /// The latest modification time in UTC.
    /// </param>
    public void UpdateFilePath(
        string filePath,
        long fileSize,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        FilePath =
            filePath;

        FileName =
            Path.GetFileNameWithoutExtension(
                filePath);

        FileSize =
            fileSize;

        CreationTimeUtc =
            creationTimeUtc;

        LastWriteTimeUtc =
            lastWriteTimeUtc;

        OnPropertyChanged(
            nameof(FilePath));

        OnPropertyChanged(
            nameof(FileName));

        OnPropertyChanged(
            nameof(FileSize));

        OnPropertyChanged(
            nameof(CreationTimeUtc));

        OnPropertyChanged(
            nameof(LastWriteTimeUtc));
    }

    /// <summary>
    /// Updates the file-system information when the file itself has changed
    /// without changing its path.
    /// </summary>
    /// <param name="fileSize">
    /// The current file size in bytes.
    /// </param>
    /// <param name="creationTimeUtc">
    /// The current file creation time in UTC.
    /// </param>
    /// <param name="lastWriteTimeUtc">
    /// The current modification time in UTC.
    /// </param>
    public void UpdateFileInfo(
        long fileSize,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc) {
        FileSize =
            fileSize;

        CreationTimeUtc =
            creationTimeUtc;

        LastWriteTimeUtc =
            lastWriteTimeUtc;

        OnPropertyChanged(
            nameof(FileSize));

        OnPropertyChanged(
            nameof(CreationTimeUtc));

        OnPropertyChanged(
            nameof(LastWriteTimeUtc));
    }

    /// <summary>
    /// Clears the current thumbnail and resets its loading state.
    /// </summary>
    public void InvalidateThumbnail() {
        Thumbnail =
            null;

        ErrorMessage =
            null;

        ThumbnailStatus =
            ThumbnailStatus.NotLoaded;
    }

    /// <summary>
    /// Clears the currently loaded model metadata so it can be loaded again.
    /// </summary>
    public void InvalidateMetadata() {
        Metadata =
            null;
    }

    /// <summary>
    /// Raised when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises PropertyChanged for the specified property.
    /// </summary>
    /// <param name="propertyName">
    /// The name of the property that changed.
    /// </param>
    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}