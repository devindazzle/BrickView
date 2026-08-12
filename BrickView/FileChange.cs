namespace BrickView;

public class FileChange
{
    public FileChangeType ChangeType { get; }

    public string FilePath { get; }

    public FileChange(
        FileChangeType changeType,
        string filePath)
    {
        ChangeType = changeType;
        FilePath = filePath;
    }
}