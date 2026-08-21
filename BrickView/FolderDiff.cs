// -----------------------------------------------------------------------------
// FolderDiff.cs
//
// Represents the complete set of detected file changes between two folder
// states.
//
// FolderDiff is a simple result model used by FolderDiffService to return the
// detected changes to its caller.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Contains the file-system changes detected for a monitored folder.
/// </summary>
public sealed class FolderDiff {
    /// <summary>
    /// Gets the changes detected between the two folder states.
    /// </summary>
    public IReadOnlyList<FileChange> Changes {
        get;
    }

    /// <summary>
    /// Creates a folder-diff result containing the specified changes.
    /// </summary>
    /// <param name="changes">
    /// The changes detected in the folder.
    /// </param>
    public FolderDiff(
        IReadOnlyList<FileChange> changes) {
        ArgumentNullException.ThrowIfNull(
            changes);

        Changes =
            changes;
    }
}