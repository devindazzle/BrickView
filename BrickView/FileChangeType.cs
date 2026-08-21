// -----------------------------------------------------------------------------
// FileChangeType.cs
//
// Defines the types of file-system changes that BrickView can detect while
// monitoring a model folder.
//
// Renamed represents the same model appearing under a different file path.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Describes the type of file-system change detected by BrickView.
/// </summary>
public enum FileChangeType {
    /// <summary>
    /// A new model file has appeared in the monitored folder.
    /// </summary>
    Added,

    /// <summary>
    /// A previously existing model file is no longer present.
    /// </summary>
    Removed,

    /// <summary>
    /// The model file is still present and has not changed.
    /// </summary>
    Unchanged,

    /// <summary>
    /// An existing model file has changed.
    /// </summary>
    Modified,

    /// <summary>
    /// An existing model file has been renamed or moved within the monitored
    /// folder while retaining its underlying model identity.
    /// </summary>
    Renamed
}