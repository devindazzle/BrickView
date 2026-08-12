using System.IO;

namespace BrickView;

public class FolderDiffService
{
    public FolderDiff Compare(
        IEnumerable<IoFileListItem> existingItems,
        IEnumerable<string> currentFiles)
    {
        Dictionary<string, IoFileListItem> existingByPath =
            existingItems.ToDictionary(
                item => item.FilePath,
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> currentFileSet =
            new HashSet<string>(
                currentFiles,
                StringComparer.OrdinalIgnoreCase);

        List<FileChange> changes =
            new List<FileChange>();

        foreach (string filePath in currentFileSet)
        {
            if (!existingByPath.TryGetValue(
                    filePath,
                    out IoFileListItem? existingItem))
            {
                changes.Add(
                    new FileChange(
                        FileChangeType.Added,
                        filePath));

                continue;
            }

            FileInfo fileInfo =
                new FileInfo(filePath);

            if (fileInfo.Length != existingItem.FileSize ||
                fileInfo.LastWriteTimeUtc !=
                    existingItem.LastWriteTimeUtc)
            {
                changes.Add(
                    new FileChange(
                        FileChangeType.Modified,
                        filePath));
            }
            else
            {
                changes.Add(
                    new FileChange(
                        FileChangeType.Unchanged,
                        filePath));
            }
        }

        foreach (IoFileListItem existingItem in existingItems)
        {
            if (!currentFileSet.Contains(
                    existingItem.FilePath))
            {
                changes.Add(
                    new FileChange(
                        FileChangeType.Removed,
                        existingItem.FilePath));
            }
        }

        return new FolderDiff(changes);
    }
}