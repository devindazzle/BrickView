using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace BrickView;

public class IoFileListItem : INotifyPropertyChanged
{
    public string FileName { get; }

    public string FilePath { get; }

    private BitmapImage? thumbnail;

    public BitmapImage? Thumbnail
    {
        get
        {
            return thumbnail;
        }

        set
        {
            if (thumbnail == value)
            {
                return;
            }

            thumbnail = value;

            OnPropertyChanged();
        }
    }

    private string? errorMessage;

    public string? ErrorMessage
    {
        get
        {
            return errorMessage;
        }

        set
        {
            if (errorMessage == value)
            {
                return;
            }

            errorMessage = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError
    {
        get
        {
            return ErrorMessage is not null;
        }
    }

    private ThumbnailStatus thumbnailStatus;

    public ThumbnailStatus ThumbnailStatus
    {
        get
        {
            return thumbnailStatus;
        }

        set
        {
            if (thumbnailStatus == value)
            {
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

    public bool IsThumbnailNotLoaded
    {
        get
        {
            return ThumbnailStatus == ThumbnailStatus.NotLoaded;
        }
    }

    public bool IsThumbnailLoading
    {
        get
        {
            return ThumbnailStatus == ThumbnailStatus.Loading;
        }
    }

    public bool IsThumbnailLoaded
    {
        get
        {
            return ThumbnailStatus == ThumbnailStatus.Loaded;
        }
    }

    public bool IsThumbnailMissing
    {
        get
        {
            return ThumbnailStatus == ThumbnailStatus.Missing;
        }
    }

    public bool IsThumbnailError
    {
        get
        {
            return ThumbnailStatus == ThumbnailStatus.Error;
        }
    }

    public IoFileListItem(
        string fileName,
        string filePath,
        BitmapImage? thumbnail,
        string? errorMessage)
    {
        FileName = fileName;
        FilePath = filePath;
        this.thumbnail = thumbnail;
        this.errorMessage = errorMessage;
        thumbnailStatus = ThumbnailStatus.NotLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}