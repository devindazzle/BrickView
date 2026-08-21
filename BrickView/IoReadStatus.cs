// -----------------------------------------------------------------------------
// IoReadStatus.cs
//
// Defines the possible outcomes when BrickView reads or validates a
// BrickLink Studio .io file.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Describes the outcome of reading or validating a BrickLink Studio .io file.
/// </summary>
public enum IoReadStatus {
    /// <summary>
    /// The .io file was read successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The requested .io file could not be found.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// The .io file is not a valid or readable ZIP archive.
    /// </summary>
    InvalidZip,

    /// <summary>
    /// Access to the .io file was denied.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// An unexpected error occurred while reading the .io file.
    /// </summary>
    UnknownError
}