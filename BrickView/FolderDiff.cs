namespace BrickView;

public class FolderDiff
{
    public IReadOnlyList<FileChange> Changes { get; }

    public FolderDiff(IReadOnlyList<FileChange> changes)
    {
        Changes = changes;
    }
}