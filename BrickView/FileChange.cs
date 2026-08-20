// -----------------------------------------------------------------------------
// FileChange.cs
//
// Represents one detected change between BrickView's current file list and
// the previous state of the monitored folder.
//
// For normal changes, FilePath identifies the affected file. For a rename,
// FilePath contains the new path and PreviousFilePath contains the path before
// the rename.
// -----------------------------------------------------------------------------

namespace BrickView;

public class FileChange {
    public FileChangeType ChangeType { get; }

    /// <summary>
    /// Gets the current file path associated with the change.
    /// For a rename, this is the new file path.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the previous file path when the change represents a rename.
    /// For all other change types this value is null.
    /// </summary>
    public string? PreviousFilePath { get; }

    public FileChange(
        FileChangeType changeType,
        string filePath) {
        ChangeType = changeType;
        FilePath = filePath;
        PreviousFilePath = null;
    }

    public FileChange(
        FileChangeType changeType,
        string filePath,
        string previousFilePath) {
        ChangeType = changeType;
        FilePath = filePath;
        PreviousFilePath = previousFilePath;
    }
}