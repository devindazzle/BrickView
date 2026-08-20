// -----------------------------------------------------------------------------
// TagPersistenceService.cs
//
// Handles persistent storage of BrickView's tag data.
//
// Tag data is stored separately from the application's general UI state in:
// %LOCALAPPDATA%\BrickView\tags.json
//
// Loaded tag data is normalized before it is returned to the application so
// that persisted data always follows BrickView's tag rules.
// -----------------------------------------------------------------------------

using System.IO;
using System.Text.Json;

namespace BrickView;

public sealed class TagPersistenceService {
    private readonly string tagStoreFilePath;

    private readonly JsonSerializerOptions jsonSerializerOptions =
        new JsonSerializerOptions {
            WriteIndented = true
        };

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

            tagStore.Normalize();

            return tagStore;
        }
        catch {
            // Tag persistence must never prevent
            // BrickView from starting.
            return new TagStore();
        }
    }

    public void Save(
        TagStore tagStore) {
        ArgumentNullException.ThrowIfNull(
            tagStore);

        try {
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
            // Tag persistence must never make
            // application shutdown fail.
        }
    }
}