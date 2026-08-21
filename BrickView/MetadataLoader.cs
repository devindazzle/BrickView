// -----------------------------------------------------------------------------
// MetadataLoader.cs
//
// Provides asynchronous loading of metadata from BrickView .io model files.
//
// Responsibilities:
// - Owns the IoFileReader used to extract model metadata.
// - Executes metadata reading asynchronously so file I/O and parsing do not
//   block the WPF UI thread.
//
// MetadataLoader is intentionally kept as a small orchestration class.
// The actual .io file reading and metadata extraction are delegated to
// IoFileReader.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Provides asynchronous access to metadata stored in BrickView .io model files.
/// </summary>
public sealed class MetadataLoader {
    private readonly IoFileReader reader;

    /// <summary>
    /// Initializes a new <see cref="MetadataLoader"/> using a dedicated
    /// <see cref="IoFileReader"/> instance.
    /// </summary>
    public MetadataLoader() {
        reader =
            new IoFileReader();
    }

    /// <summary>
    /// Loads model metadata from the specified .io file asynchronously.
    /// </summary>
    /// <param name="filePath">
    /// The full path to the .io file whose metadata should be loaded.
    /// </param>
    /// <returns>
    /// A task that completes with the extracted <see cref="IoModelMetadata"/>,
    /// or <see langword="null"/> when no metadata can be returned.
    /// </returns>
    public Task<IoModelMetadata?> LoadAsync(string filePath) {
        // Run the synchronous reader on a background thread so callers on the
        // WPF UI thread can await the operation without blocking the interface.
        return Task.Run(
            () => reader.ReadMetadata(
                filePath));
    }
}