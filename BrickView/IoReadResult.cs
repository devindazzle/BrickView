namespace BrickView;

public class IoReadResult
{
    public IoReadStatus Status { get; }

    public bool ThumbnailFound { get; }

    public string? ErrorMessage { get; }

    public bool IsSuccess
    {
        get
        {
            return Status == IoReadStatus.Success;
        }
    }

    public IoReadResult(
        IoReadStatus status,
        bool thumbnailFound,
        string? errorMessage = null)
    {
        Status = status;
        ThumbnailFound = thumbnailFound;
        ErrorMessage = errorMessage;
    }
}