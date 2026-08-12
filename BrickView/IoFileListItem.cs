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