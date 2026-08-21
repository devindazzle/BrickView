// -----------------------------------------------------------------------------
// ThumbnailReadResult.cs
//
// Represents the result of attempting to read thumbnail data from a BrickView
// model file.
//
// Responsibilities:
// - Reports the outcome of the thumbnail read operation.
// - Carries the resulting thumbnail data when the read succeeds.
// - Carries an explanatory error message when the read fails.
//
// This class is a data-transfer object between IoFileReader and the thumbnail
// loading infrastructure. It does not perform file I/O, image decoding or
// error handling itself.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents the result of an attempt to read thumbnail data from a model file.
/// </summary>
public class ThumbnailReadResult {
    /// <summary>
    /// Gets the status describing the outcome of the thumbnail read operation.
    /// </summary>
    public ThumbnailReadStatus Status {
        get;
    }

    /// <summary>
    /// Gets the raw thumbnail image data when the thumbnail was successfully read.
    /// </summary>
    /// <remarks>
    /// This value is <see langword="null"/> when no thumbnail data was returned.
    /// </remarks>
    public byte[]? Data {
        get;
    }

    /// <summary>
    /// Gets the error message describing why the thumbnail could not be read,
    /// when an error occurred.
    /// </summary>
    /// <remarks>
    /// This value is <see langword="null"/> when no error occurred or when the
    /// caller did not provide an error message.
    /// </remarks>
    public string? ErrorMessage {
        get;
    }

    /// <summary>
    /// Initializes a new thumbnail read result.
    /// </summary>
    /// <param name="status">
    /// The status of the thumbnail read operation.
    /// </param>
    /// <param name="data">
    /// The raw thumbnail image data, when available.
    /// </param>
    /// <param name="errorMessage">
    /// An optional error message describing a failed read operation.
    /// </param>
    public ThumbnailReadResult(
        ThumbnailReadStatus status,
        byte[]? data = null,
        string? errorMessage = null) {
        Status =
            status;

        Data =
            data;

        ErrorMessage =
            errorMessage;
    }
}