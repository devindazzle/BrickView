// -----------------------------------------------------------------------------
// ThumbnailSizeManager.cs
//
// Provides the application-wide manager for the currently selected thumbnail
// size in BrickView.
//
// Responsibilities:
// - Maintains the currently active ThumbnailSizeDefinition.
// - Provides access to the shared ThumbnailSizeManager instance.
// - Changes the active thumbnail size from a ThumbnailSizePreset.
// - Notifies subscribers when the active thumbnail size changes.
//
// ThumbnailSizeManager uses the predefined dimensions from ThumbnailSizes.
// It does not define dimensions itself and contains no UI-specific layout logic.
//
// The manager starts with the Medium thumbnail size as the default.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Manages the currently selected thumbnail size for BrickView.
/// </summary>
public sealed class ThumbnailSizeManager {
    private static readonly ThumbnailSizeManager instance =
        new ThumbnailSizeManager();

    private ThumbnailSizeDefinition currentSize;

    /// <summary>
    /// Initializes the thumbnail size manager with the default Medium size.
    /// </summary>
    private ThumbnailSizeManager() {
        currentSize =
            ThumbnailSizes.Medium;
    }

    /// <summary>
    /// Gets the shared application-wide thumbnail size manager instance.
    /// </summary>
    public static ThumbnailSizeManager Instance {
        get {
            return instance;
        }
    }

    /// <summary>
    /// Gets the thumbnail size definition that is currently active.
    /// </summary>
    public ThumbnailSizeDefinition Current {
        get {
            return currentSize;
        }
    }

    /// <summary>
    /// Occurs when the active thumbnail size changes.
    /// </summary>
    /// <remarks>
    /// The event provides the newly selected thumbnail size definition.
    /// </remarks>
    public event Action<ThumbnailSizeDefinition>? SizeChanged;

    /// <summary>
    /// Changes the active thumbnail size to the definition represented by
    /// the specified preset.
    /// </summary>
    /// <param name="preset">
    /// The thumbnail size preset to activate.
    /// </param>
    /// <remarks>
    /// No event is raised when the requested preset is already active.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="preset"/> does not represent a known
    /// thumbnail size preset.
    /// </exception>
    public void SetSize(
        ThumbnailSizePreset preset) {
        ThumbnailSizeDefinition newSize =
            GetDefinition(
                preset);

        if (ReferenceEquals(
                currentSize,
                newSize)) {
            return;
        }

        currentSize =
            newSize;

        // Notify subscribers only after the new size has become the current
        // shared state so handlers always observe the updated definition.
        SizeChanged?.Invoke(
            currentSize);
    }

    /// <summary>
    /// Resolves a thumbnail size preset to its predefined size definition.
    /// </summary>
    /// <param name="preset">
    /// The thumbnail size preset to resolve.
    /// </param>
    /// <returns>
    /// The predefined thumbnail size definition associated with the preset.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="preset"/> is not a known thumbnail size preset.
    /// </exception>
    private static ThumbnailSizeDefinition GetDefinition(
        ThumbnailSizePreset preset) {
        return preset switch {
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