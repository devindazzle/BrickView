namespace BrickView;

public enum ThumbnailStatus
{
    /// <summary>
    /// Thumbnail is not yet loaded.
    /// </summary>
    NotLoaded,
    /// <summary>
    /// Thumbnails is being loaded right now.
    /// </summary>
    Loading,
    /// <summary>
    /// Thumbnail has been loaded already.
    /// </summary>
    Loaded,
    /// <summary>
    /// The .io file does not contain a thumbnail image.
    /// </summary>
    Missing,
    /// <summary>
    /// Something went wrong during the load of the .io file.
    /// </summary>
    Error
}