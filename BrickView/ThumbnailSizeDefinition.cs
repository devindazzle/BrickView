// -----------------------------------------------------------------------------
// ThumbnailSizeDefinition.cs
//
// Defines the available thumbnail size presets used by BrickView.
//
// Each preset contains:
// - Thumbnail dimensions
// - Card dimensions
// - Virtualized item dimensions
//
// Card and item heights include the additional vertical space required by the
// model card content below the thumbnail.
// -----------------------------------------------------------------------------

namespace BrickView;

public enum ThumbnailSizePreset {
    Small,
    Medium,
    Large
}

public sealed class ThumbnailSizeDefinition {
    public ThumbnailSizeDefinition(
        ThumbnailSizePreset preset,
        double thumbnailWidth,
        double thumbnailHeight,
        double cardWidth,
        double cardHeight,
        double itemWidth,
        double itemHeight) {
        Preset = preset;
        ThumbnailWidth = thumbnailWidth;
        ThumbnailHeight = thumbnailHeight;
        CardWidth = cardWidth;
        CardHeight = cardHeight;
        ItemWidth = itemWidth;
        ItemHeight = itemHeight;
    }

    public ThumbnailSizePreset Preset {
        get;
    }

    public double ThumbnailWidth {
        get;
    }

    public double ThumbnailHeight {
        get;
    }

    public double CardWidth {
        get;
    }

    public double CardHeight {
        get;
    }

    public double ItemWidth {
        get;
    }

    public double ItemHeight {
        get;
    }
}

public static class ThumbnailSizes {
    public static ThumbnailSizeDefinition Small {
        get;
    } =
        new ThumbnailSizeDefinition(
            ThumbnailSizePreset.Small,
            240,
            160,
            240,
            234,
            258,
            254);

    public static ThumbnailSizeDefinition Medium {
        get;
    } =
        new ThumbnailSizeDefinition(
            ThumbnailSizePreset.Medium,
            360,
            240,
            360,
            314,
            378,
            334);

    public static ThumbnailSizeDefinition Large {
        get;
    } =
        new ThumbnailSizeDefinition(
            ThumbnailSizePreset.Large,
            480,
            320,
            480,
            394,
            498,
            414);
}