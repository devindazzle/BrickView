// -----------------------------------------------------------------------------
// ThumbnailLoadPriority.cs
//
// Defines the priority levels used by BrickView's thumbnail loading queue.
//
// A visible model thumbnail receives higher priority than a preload request so
// thumbnails currently needed by the UI are processed before thumbnails loaded
// speculatively for later use.
//
// The numeric values are significant because ThumbnailLoader uses them when
// calculating the priority of entries in its PriorityQueue.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Defines the priority assigned to a thumbnail load request.
/// </summary>
public enum ThumbnailLoadPriority {
    /// <summary>
    /// Indicates that the thumbnail is currently visible and should be loaded
    /// with the highest priority.
    /// </summary>
    Visible = 0,

    /// <summary>
    /// Indicates that the thumbnail is being loaded speculatively for possible
    /// later use and therefore has lower priority than a visible thumbnail.
    /// </summary>
    Preload = 1
}