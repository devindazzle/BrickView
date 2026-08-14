using System.IO;
using System.Text.Json;

namespace BrickView;

public sealed class ApplicationStateService
{
    private readonly string stateFilePath;

    private readonly JsonSerializerOptions jsonSerializerOptions =
        new JsonSerializerOptions
        {
            WriteIndented = true
        };

    public ApplicationStateService()
    {
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

    public ApplicationState? Load()
    {
        try
        {
            if (!File.Exists(
                    stateFilePath))
            {
                return null;
            }

            string json =
                File.ReadAllText(
                    stateFilePath);

            return JsonSerializer.Deserialize<ApplicationState>(
                json,
                jsonSerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(
        ApplicationState state)
    {
        try
        {
            string json =
                JsonSerializer.Serialize(
                    state,
                    jsonSerializerOptions);

            File.WriteAllText(
                stateFilePath,
                json);
        }
        catch
        {
            // Persistence must never make
            // application shutdown fail.
        }
    }
}