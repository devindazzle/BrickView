// -----------------------------------------------------------------------------
// ThumbnailStatus.cs
//
// Defines the runtime status of a model thumbnail in BrickView.
//
// The status describes the thumbnail loading lifecycle from the initial state
// through loading, successful completion, missing thumbnail data or a load
// error.
//
// ThumbnailStatus is used by the thumbnail-loading and model-listing
// infrastructure to determine how a thumbnail should be handled or displayed.
// It does not contain any loading logic itself.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents the current loading status of a model thumbnail.
/// </summary>
public enum ThumbnailStatus {
    /// <summary>
    /// Indicates that the thumbnail has not yet been loaded.
    /// </summary>
    NotLoaded,

    /// <summary>
    /// Indicates that the thumbnail is currently being loaded.
    /// </summary>
    Loading,

    /// <summary>
    /// Indicates that the thumbnail has been loaded successfully.
    /// </summary>
    Loaded,

    /// <summary>
    /// Indicates that the .io file does not contain a thumbnail image.
    /// </summary>
    Missing,

    /// <summary>
    /// Indicates that an error occurred while loading the thumbnail or
    /// reading the corresponding .io file.
    /// </summary>
    Error
}