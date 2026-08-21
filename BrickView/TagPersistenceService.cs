// -----------------------------------------------------------------------------
// TagPersistenceService.cs
//
// Provides persistent storage for BrickView's tag data.
//
// Responsibilities:
// - Determines the location of BrickView's persistent tag store.
// - Loads tag data from %LOCALAPPDATA%\BrickView\tags.json.
// - Normalizes loaded tag data before returning it to the application.
// - Serializes and saves the current tag store.
// - Prevents tag-persistence failures from preventing BrickView from starting
//   or shutting down.
//
// Tag persistence is deliberately kept separate from the application's general
// UI state. This service owns file-based persistence only; tag creation,
// assignment and in-memory tag management are handled by the surrounding tag
// infrastructure.
//
// Persistence failures are intentionally non-fatal. BrickView can operate with
// an empty tag store when loading fails and can complete shutdown when saving
// fails.
// -----------------------------------------------------------------------------

using System.IO;
using System.Text.Json;

namespace BrickView;

/// <summary>
/// Provides persistent storage for BrickView's tag data.
/// </summary>
public sealed class TagPersistenceService {
    private readonly string tagStoreFilePath;

    private readonly JsonSerializerOptions jsonSerializerOptions =
        new JsonSerializerOptions {
            WriteIndented = true
        };

    /// <summary>
    /// Initializes the tag persistence service and determines the path of the
    /// persistent tag store.
    /// </summary>
    /// <remarks>
    /// The BrickView application-data directory is created when necessary so
    /// subsequent save operations can write the tag store without requiring
    /// additional directory setup.
    /// </remarks>
    public TagPersistenceService() {
        string applicationDataFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BrickView");

        Directory.CreateDirectory(
            applicationDataFolder);

        tagStoreFilePath =
            Path.Combine(
                applicationDataFolder,
                "tags.json");
    }

    /// <summary>
    /// Loads the persisted BrickView tag store.
    /// </summary>
    /// <returns>
    /// The normalized persisted tag store, or a new empty
    /// <see cref="TagStore"/> when no usable persisted data is available.
    /// </returns>
    /// <remarks>
    /// Loading errors are intentionally treated as non-fatal so corrupted,
    /// missing or inaccessible tag persistence cannot prevent BrickView
    /// from starting.
    /// </remarks>
    public TagStore Load() {
        try {
            if (!File.Exists(
                    tagStoreFilePath)) {
                return new TagStore();
            }

            string json =
                File.ReadAllText(
                    tagStoreFilePath);

            TagStore? tagStore =
                JsonSerializer.Deserialize<TagStore>(
                    json,
                    jsonSerializerOptions);

            if (tagStore is null) {
                return new TagStore();
            }

            // Normalize persisted data before exposing it to the rest of the
            // application so loaded tags follow the same domain rules as new tags.
            tagStore.Normalize();

            return tagStore;
        }
        catch {
            // Tag persistence must never prevent BrickView from starting.
            // Returning an empty store allows the rest of the application
            // to operate normally even when persisted data cannot be loaded.
            return new TagStore();
        }
    }

    /// <summary>
    /// Saves the supplied tag store to BrickView's persistent tag file.
    /// </summary>
    /// <param name="tagStore">
    /// The tag store to normalize and persist.
    /// </param>
    /// <remarks>
    /// The tag store is normalized before serialization so persisted data
    /// remains consistent with BrickView's tag-domain rules.
    ///
    /// Save failures are intentionally ignored because tag persistence must
    /// never prevent the application from shutting down successfully.
    /// </remarks>
    public void Save(
        TagStore tagStore) {
        ArgumentNullException.ThrowIfNull(
            tagStore);

        try {
            // Normalize before persistence so data written to disk follows
            // the same rules as data loaded into the application.
            tagStore.Normalize();

            string json =
                JsonSerializer.Serialize(
                    tagStore,
                    jsonSerializerOptions);

            File.WriteAllText(
                tagStoreFilePath,
                json);
        }
        catch {
            // Tag persistence must never make application shutdown fail.
            // The application state remains usable even when the tag store
            // cannot be written.
        }
    }
}