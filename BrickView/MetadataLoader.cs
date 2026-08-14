namespace BrickView;

public sealed class MetadataLoader
{
    private readonly IoFileReader reader;

    public MetadataLoader()
    {
        reader =
            new IoFileReader();
    }

    public Task<IoModelMetadata?> LoadAsync(string filePath)
    { 
        return Task.Run(
            () => reader.ReadMetadata(
                filePath));
    }
}