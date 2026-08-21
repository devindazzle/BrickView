// -----------------------------------------------------------------------------
// ThumbnailSizeDefinition.cs
//
// Defines the available thumbnail size presets used by BrickView.
//
// This file contains:
// - ThumbnailSizePreset
//     Identifies the available thumbnail size presets.
//
// - ThumbnailSizeDefinition
//     Describes the thumbnail, model-card and virtualized-item dimensions
//     associated with one preset.
//
// - ThumbnailSizes
//     Provides the predefined Small, Medium and Large size definitions.
//
// Each preset contains:
// - Thumbnail dimensions.
// - Card dimensions.
// - Virtualized item dimensions.
//
// Card and item heights include the additional vertical space required by the
// model card content below the thumbnail.
//
// These definitions provide shared layout dimensions for the thumbnail and
// model-listing infrastructure. They do not contain UI logic.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Identifies the available thumbnail size presets in BrickView.
/// </summary>
public enum ThumbnailSizePreset {
    /// <summary>
    /// The smallest available thumbnail size.
    /// </summary>
    Small,

    /// <summary>
    /// The default medium thumbnail size.
    /// </summary>
    Medium,

    /// <summary>
    /// The largest available thumbnail size.
    /// </summary>
    Large
}

/// <summary>
/// Defines the thumbnail, model-card and virtualized-item dimensions for one
/// thumbnail size preset.
/// </summary>
public sealed class ThumbnailSizeDefinition {
    /// <summary>
    /// Gets the thumbnail size preset represented by this definition.
    /// </summary>
    public ThumbnailSizePreset Preset {
        get;
    }

    /// <summary>
    /// Gets the width of the rendered thumbnail in device-independent pixels.
    /// </summary>
    public double ThumbnailWidth {
        get;
    }

    /// <summary>
    /// Gets the height of the rendered thumbnail in device-independent pixels.
    /// </summary>
    public double ThumbnailHeight {
        get;
    }

    /// <summary>
    /// Gets the width of the model card associated with this thumbnail size.
    /// </summary>
    public double CardWidth {
        get;
    }

    /// <summary>
    /// Gets the total height of the model card, including content below the
    /// thumbnail.
    /// </summary>
    public double CardHeight {
        get;
    }

    /// <summary>
    /// Gets the width used for the corresponding virtualized item.
    /// </summary>
    public double ItemWidth {
        get;
    }

    /// <summary>
    /// Gets the total height used for the corresponding virtualized item,
    /// including content below the thumbnail.
    /// </summary>
    public double ItemHeight {
        get;
    }

    /// <summary>
    /// Initializes a thumbnail size definition with the supplied dimensions.
    /// </summary>
    /// <param name="preset">
    /// The thumbnail size preset represented by the definition.
    /// </param>
    /// <param name="thumbnailWidth">
    /// The thumbnail width.
    /// </param>
    /// <param name="thumbnailHeight">
    /// The thumbnail height.
    /// </param>
    /// <param name="cardWidth">
    /// The model card width.
    /// </param>
    /// <param name="cardHeight">
    /// The total model card height.
    /// </param>
    /// <param name="itemWidth">
    /// The virtualized item width.
    /// </param>
    /// <param name="itemHeight">
    /// The total virtualized item height.
    /// </param>
    public ThumbnailSizeDefinition(
        ThumbnailSizePreset preset,
        double thumbnailWidth,
        double thumbnailHeight,
        double cardWidth,
        double cardHeight,
        double itemWidth,
        double itemHeight) {
        Preset =
            preset;

        ThumbnailWidth =
            thumbnailWidth;

        ThumbnailHeight =
            thumbnailHeight;

        CardWidth =
            cardWidth;

        CardHeight =
            cardHeight;

        ItemWidth =
            itemWidth;

        ItemHeight =
            itemHeight;
    }
}

/// <summary>
/// Provides the predefined thumbnail size definitions used by BrickView.
/// </summary>
public static class ThumbnailSizes {
    /// <summary>
    /// Gets the Small thumbnail size definition.
    /// </summary>
    /// <remarks>
    /// Thumbnail: 240 × 160.
    /// Card: 240 × 234.
    /// Virtualized item: 258 × 254.
    /// </remarks>
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

    /// <summary>
    /// Gets the Medium thumbnail size definition.
    /// </summary>
    /// <remarks>
    /// Thumbnail: 360 × 240.
    /// Card: 360 × 314.
    /// Virtualized item: 378 × 334.
    /// </remarks>
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

    /// <summary>
    /// Gets the Large thumbnail size definition.
    /// </summary>
    /// <remarks>
    /// Thumbnail: 480 × 320.
    /// Card: 480 × 394.
    /// Virtualized item: 498 × 414.
    /// </remarks>
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