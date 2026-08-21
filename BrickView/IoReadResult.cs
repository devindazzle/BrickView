// -----------------------------------------------------------------------------
// IoReadResult.cs
//
// Represents the result of validating and inspecting a BrickLink Studio .io
// file.
//
// The result reports the operation status, whether a thumbnail was found and
// an optional error message. IoFileReader creates this result and callers can
// use IsSuccess for a convenient success check.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Contains the result of reading or validating a BrickLink Studio .io file.
/// </summary>
public sealed class IoReadResult {
    /// <summary>
    /// Gets the status returned by the .io file reader.
    /// </summary>
    public IoReadStatus Status {
        get;
    }

    /// <summary>
    /// Gets whether a thumbnail entry was found in the .io archive.
    /// </summary>
    public bool ThumbnailFound {
        get;
    }

    /// <summary>
    /// Gets an optional error message describing why the operation failed.
    /// </summary>
    public string? ErrorMessage {
        get;
    }

    /// <summary>
    /// Gets whether the .io file was read successfully.
    /// </summary>
    public bool IsSuccess {
        get {
            return Status ==
                   IoReadStatus.Success;
        }
    }

    /// <summary>
    /// Creates an .io file read result.
    /// </summary>
    /// <param name="status">
    /// The result status.
    /// </param>
    /// <param name="thumbnailFound">
    /// Indicates whether a thumbnail was found.
    /// </param>
    /// <param name="errorMessage">
    /// An optional error message.
    /// </param>
    public IoReadResult(
        IoReadStatus status,
        bool thumbnailFound,
        string? errorMessage = null) {
        Status =
            status;

        ThumbnailFound =
            thumbnailFound;

        ErrorMessage =
            errorMessage;
    }
}