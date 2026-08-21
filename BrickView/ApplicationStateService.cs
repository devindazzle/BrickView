// -----------------------------------------------------------------------------
// ApplicationStateService.cs
//
// Provides persistent storage for BrickView's application-level state.
//
// The service stores the state in the user's local application-data folder as
// a JSON file. It is responsible only for serializing and deserializing
// ApplicationState; the ApplicationState model itself contains the actual
// persisted values.
//
// Persistence failures are intentionally handled without propagating
// exceptions so that a damaged or unavailable state file cannot prevent
// BrickView from starting or shutting down normally.
// -----------------------------------------------------------------------------

using System.IO;
using System.Text.Json;

namespace BrickView;

public sealed class ApplicationStateService {
    private readonly string stateFilePath;

    private readonly JsonSerializerOptions jsonSerializerOptions =
        new JsonSerializerOptions {
            WriteIndented = true
        };

    /// <summary>
    /// Initializes the service and determines the location of the persistent
    /// application state file.
    /// </summary>
    public ApplicationStateService() {
        string applicationDataFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BrickView");

        Directory.CreateDirectory(
            applicationDataFolder);

        stateFilePath =
            Path.Combine(
                applicationDataFolder,
                "state.json");
    }

    /// <summary>
    /// Loads the previously persisted application state.
    /// </summary>
    /// <returns>
    /// The stored application state, or null when no valid state is available.
    /// </returns>
    public ApplicationState? Load() {
        try {
            if (!File.Exists(
                    stateFilePath)) {
                return null;
            }

            string json =
                File.ReadAllText(
                    stateFilePath);

            return JsonSerializer.Deserialize<ApplicationState>(
                json,
                jsonSerializerOptions);
        }
        catch {
            // A missing, invalid or unreadable state file should not prevent
            // BrickView from starting. The application can fall back to its
            // default state.
            return null;
        }
    }

    /// <summary>
    /// Persists the supplied application state as formatted JSON.
    /// </summary>
    /// <param name="state">
    /// The application state to persist.
    /// </param>
    public void Save(
        ApplicationState state) {
        ArgumentNullException.ThrowIfNull(
            state);

        try {
            string json =
                JsonSerializer.Serialize(
                    state,
                    jsonSerializerOptions);

            File.WriteAllText(
                stateFilePath,
                json);
        }
        catch {
            // Persistence must never make application shutdown fail.
        }
    }
}