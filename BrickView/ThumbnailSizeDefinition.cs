namespace BrickView;

public enum ThumbnailSizePreset
{
    Small,
    Medium,
    Large
}

public sealed class ThumbnailSizeDefinition
{
    public ThumbnailSizeDefinition(
        ThumbnailSizePreset preset,
        double thumbnailWidth,
        double thumbnailHeight,
        double cardWidth,
        double cardHeight,
        double itemWidth,
        double itemHeight)
    {
        Preset = preset;
        ThumbnailWidth = thumbnailWidth;
        ThumbnailHeight = thumbnailHeight;
        CardWidth = cardWidth;
        CardHeight = cardHeight;
        ItemWidth = itemWidth;
        ItemHeight = itemHeight;
    }

    public ThumbnailSizePreset Preset
    {
        get;
    }

    public double ThumbnailWidth
    {
        get;
    }

    public double ThumbnailHeight
    {
        get;
    }

    public double CardWidth
    {
        get;
    }

    public double CardHeight
    {
        get;
    }

    public double ItemWidth
    {
        get;
    }

    public double ItemHeight
    {
        get;
    }
}

public static class ThumbnailSizes
{
    public static ThumbnailSizeDefinition Small
    {
        get;
    } =
        new ThumbnailSizeDefinition(
            ThumbnailSizePreset.Small,
            240,
            160,
            240,
            210,
            258,
            230);

    public static ThumbnailSizeDefinition Medium
    {
        get;
    } =
        new ThumbnailSizeDefinition(
            ThumbnailSizePreset.Medium,
            360,
            240,
            360,
            290,
            378,
            310);

    public static ThumbnailSizeDefinition Large
    {
        get;
    } =
        new ThumbnailSizeDefinition(
            ThumbnailSizePreset.Large,
            480,
            320,
            480,
            370,
            498,
            390);
}