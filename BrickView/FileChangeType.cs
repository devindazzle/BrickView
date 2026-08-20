// -----------------------------------------------------------------------------
// FileChangeType.cs
//
// Defines the types of file system changes that BrickView can detect while
// monitoring a model folder.
//
// Renamed represents the same model appearing under a different file path.
// -----------------------------------------------------------------------------

namespace BrickView;

public enum FileChangeType {
    Added,

    Removed,

    Unchanged,

    Modified,

    Renamed
}