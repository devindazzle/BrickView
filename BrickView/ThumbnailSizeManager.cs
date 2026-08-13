namespace BrickView;

public sealed class ThumbnailSizeManager
{
    private static readonly ThumbnailSizeManager instance =
        new ThumbnailSizeManager();

    private ThumbnailSizeDefinition currentSize;

    private ThumbnailSizeManager()
    {
        currentSize =
            ThumbnailSizes.Medium;
    }

    public static ThumbnailSizeManager Instance
    {
        get
        {
            return instance;
        }
    }

    public ThumbnailSizeDefinition Current
    {
        get
        {
            return currentSize;
        }
    }

    public event Action<ThumbnailSizeDefinition>? SizeChanged;

    public void SetSize(
        ThumbnailSizePreset preset)
    {
        ThumbnailSizeDefinition newSize =
            GetDefinition(
                preset);

        if (ReferenceEquals(
                currentSize,
                newSize))
        {
            return;
        }

        currentSize =
            newSize;

        SizeChanged?.Invoke(
            currentSize);
    }

    private static ThumbnailSizeDefinition GetDefinition(
        ThumbnailSizePreset preset)
    {
        return preset switch
        {
            ThumbnailSizePreset.Small =>
                ThumbnailSizes.Small,

            ThumbnailSizePreset.Medium =>
                ThumbnailSizes.Medium,

            ThumbnailSizePreset.Large =>
                ThumbnailSizes.Large,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(preset),
                    preset,
                    "Unknown thumbnail size preset.")
        };
    }
}