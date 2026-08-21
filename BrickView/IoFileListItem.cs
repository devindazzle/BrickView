// -----------------------------------------------------------------------------
// IoFileListItem.cs
//
// Represents one .io file displayed by BrickView.
//
// The class contains the file information used by the UI together with
// thumbnail and metadata state. A stable ModelIdentity is also associated
// with the item so that data such as tags can remain attached when the file
// is renamed.
//
// Windows-specific file identity resolution is intentionally kept outside
// this class. IoFileListItem only stores the resulting ModelIdentity.
//
// Tags are stored as a read-only snapshot supplied by the tag service. The
// item does not access persistence or tag services directly, keeping the file
// model independent of the tag system's storage implementation.
// -----------------------------------------------------------------------------

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace BrickView;

public class IoFileListItem : INotifyPropertyChanged {
    public string FileName { get; private set; }

    public string FilePath { get; private set; }

    public long FileSize { get; private set; }

    public DateTime CreationTimeUtc { get; private set; }

    public DateTime LastWriteTimeUtc { get; private set; }

    // The stable model identity is independent of the current file path.
    // It is assigned by the file identity layer when that identity is known.
    public ModelIdentity? ModelIdentity { get; private set; }

    private IReadOnlyList<TagDefinition> tags;

    public IReadOnlyList<TagDefinition> Tags {
        get {
            return tags;
        }
    }

    private bool isFavorite;

    public bool IsFavorite {
        get {
            return isFavorite;
        }

        set {
            if (isFavorite == value) {
                return;
            }

            isFavorite = value;

            OnPropertyChanged();
        }
    }

    private IoModelMetadata? metadata;

    public IoModelMetadata? Metadata {
        get {
            return metadata;
        }

        set {
            if (metadata == value) {
                return;
            }

            metadata = value;

            OnPropertyChanged();
        }
    }

    private BitmapImage? thumbnail;

    public BitmapImage? Thumbnail {
        get {
            return thumbnail;
        }

        set {
            if (thumbnail == value) {
                return;
            }

            thumbnail = value;

            OnPropertyChanged();
        }
    }

    private string? errorMessage;

    public string? ErrorMessage {
        get {
            return errorMessage;
        }

        set {
            if (errorMessage == value) {
                return;
            }

            errorMessage = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError {
        get {
            return ErrorMessage is not null;
        }
    }

    private ThumbnailStatus thumbnailStatus;

    public ThumbnailStatus ThumbnailStatus {
        get {
            return thumbnailStatus;
        }

        set {
            if (thumbnailStatus == value) {
                return;
            }

            thumbnailStatus = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsThumbnailNotLoaded));
            OnPropertyChanged(nameof(IsThumbnailLoading));
            OnPropertyChanged(nameof(IsThumbnailLoaded));
            OnPropertyChanged(nameof(IsThumbnailMissing));
            OnPropertyChanged(nameof(IsThumbnailError));
        }
    }

    public bool IsThumbnailNotLoaded {
        get {
            return ThumbnailStatus == ThumbnailStatus.NotLoaded;
        }
    }

    public bool IsThumbnailLoading {
        get {
            return ThumbnailStatus == ThumbnailStatus.Loading;
        }
    }

    public bool IsThumbnailLoaded {
        get {
            return ThumbnailStatus == ThumbnailStatus.Loaded;
        }
    }

    public bool IsThumbnailMissing {
        get {
            return ThumbnailStatus == ThumbnailStatus.Missing;
        }
    }

    public bool IsThumbnailError {
        get {
            return ThumbnailStatus == ThumbnailStatus.Error;
        }
    }

    public IoFileListItem(
        string fileName,
        string filePath,
        long fileSize,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc,
        BitmapImage? thumbnail,
        string? errorMessage) {
        FileName = fileName;
        FilePath = filePath;
        FileSize = fileSize;
        CreationTimeUtc = creationTimeUtc;
        LastWriteTimeUtc = lastWriteTimeUtc;
        this.thumbnail = thumbnail;
        this.errorMessage = errorMessage;
        ModelIdentity = null;
        tags =
            Array.Empty<TagDefinition>();
        isFavorite = false;
        metadata = null;
        thumbnailStatus = ThumbnailStatus.NotLoaded;
    }

    public void SetModelIdentity(
        ModelIdentity modelIdentity) {
        ArgumentNullException.ThrowIfNull(
            modelIdentity);

        if (ModelIdentity == modelIdentity) {
            return;
        }

        // The identity is assigned once when the file is discovered. Keeping
        // it separate from FilePath allows the same model to survive a rename.
        ModelIdentity =
            modelIdentity;

        OnPropertyChanged();
    }

    public void SetTags(
        IReadOnlyList<TagDefinition> tags) {
        ArgumentNullException.ThrowIfNull(
            tags);

        // Create a new read-only snapshot instead of keeping the collection
        // instance owned by ModelTagCollection. This guarantees that each tag
        // update gives WPF a new ItemsSource value and therefore refreshes the
        // tag badges and Tags.Count binding immediately.
        IReadOnlyList<TagDefinition> newTags =
            tags.ToArray();

        this.tags =
            newTags;

        OnPropertyChanged(
            nameof(Tags));
    }

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

    public void UpdateFileInfo(
        long fileSize,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc) {
        FileSize = fileSize;
        CreationTimeUtc = creationTimeUtc;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }

    public void InvalidateThumbnail() {
        Thumbnail = null;
        ErrorMessage = null;
        ThumbnailStatus = ThumbnailStatus.NotLoaded;
    }

    public void InvalidateMetadata() {
        Metadata = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}