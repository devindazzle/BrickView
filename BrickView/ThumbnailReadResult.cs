namespace BrickView;

public class ThumbnailReadResult
{
    public ThumbnailReadStatus Status { get; }

    public byte[]? Data { get; }

    public string? ErrorMessage { get; }

    public ThumbnailReadResult(
        ThumbnailReadStatus status,
        byte[]? data = null,
        string? errorMessage = null)
    {
        Status = status;
        Data = data;
        ErrorMessage = errorMessage;
    }
}