// -----------------------------------------------------------------------------
// ThumbnailReadStatus.cs
//
// Defines the possible outcomes when BrickView attempts to read a thumbnail
// from a model file.
//
// The status is used by the thumbnail-reading layer to communicate whether
// thumbnail data was successfully loaded, was not present, could not be read
// because the file was invalid, or failed for another reason.
//
// ThumbnailReadResult combines this status with optional thumbnail data and
// an optional error message.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents the outcome of an attempt to read thumbnail data from a model file.
/// </summary>
public enum ThumbnailReadStatus {
    /// <summary>
    /// Indicates that thumbnail data was successfully read.
    /// </summary>
    Loaded,

    /// <summary>
    /// Indicates that the model file does not contain a thumbnail.
    /// </summary>
    Missing,

    /// <summary>
    /// Indicates that the model file is not a valid file format for thumbnail reading.
    /// </summary>
    InvalidFile,

    /// <summary>
    /// Indicates that an error occurred while attempting to read the thumbnail.
    /// </summary>
    Error
}