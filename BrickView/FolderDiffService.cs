// -----------------------------------------------------------------------------
// FolderDiffService.cs
//
// Compares BrickView's existing file items with the files currently present
// in a monitored folder.
//
// The service detects added, removed, modified, unchanged and renamed files.
// Rename detection uses the stable ModelIdentity assigned to IoFileListItem.
//
// File identity lookups are performed asynchronously so that large folders do
// not cause Windows file-system operations to block the UI thread.
// -----------------------------------------------------------------------------

using System.IO;

namespace BrickView;

public sealed class FolderDiffService {
    private readonly WindowsFileIdentityProvider fileIdentityProvider;

    public FolderDiffService(
        WindowsFileIdentityProvider fileIdentityProvider) {
        ArgumentNullException.ThrowIfNull(
            fileIdentityProvider);

        this.fileIdentityProvider =
            fileIdentityProvider;
    }

    public async Task<FolderDiff> CompareAsync(
        IEnumerable<IoFileListItem> existingItems,
        IEnumerable<string> currentFiles,
        CancellationToken cancellationToken = default) {
        List<IoFileListItem> existingItemList =
            existingItems.ToList();

        HashSet<string> currentFileSet =
            new HashSet<string>(
                currentFiles,
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, IoFileListItem> existingByPath =
            existingItemList.ToDictionary(
                item => item.FilePath,
                StringComparer.OrdinalIgnoreCase);

        List<FileChange> changes =
            new List<FileChange>();

        List<string> addedFiles =
            new List<string>();

        foreach (string filePath in currentFileSet) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!existingByPath.TryGetValue(
                    filePath,
                    out IoFileListItem? existingItem)) {
                addedFiles.Add(
                    filePath);

                continue;
            }

            FileInfo fileInfo =
                new FileInfo(filePath);

            if (fileInfo.Length != existingItem.FileSize ||
                fileInfo.LastWriteTimeUtc !=
                    existingItem.LastWriteTimeUtc) {
                changes.Add(
                    new FileChange(
                        FileChangeType.Modified,
                        filePath));
            }
            else {
                changes.Add(
                    new FileChange(
                        FileChangeType.Unchanged,
                        filePath));
            }
        }

        List<IoFileListItem> removedItems =
            new List<IoFileListItem>();

        foreach (IoFileListItem existingItem
                 in existingItemList) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!currentFileSet.Contains(
                    existingItem.FilePath)) {
                removedItems.Add(
                    existingItem);
            }
        }

        await DetectRenamesAsync(
            addedFiles,
            removedItems,
            changes,
            cancellationToken);

        foreach (string filePath in addedFiles) {
            cancellationToken.ThrowIfCancellationRequested();

            bool wasRenamed =
                changes.Any(
                    change =>
                        change.ChangeType ==
                            FileChangeType.Renamed &&
                        string.Equals(
                            change.FilePath,
                            filePath,
                            StringComparison.OrdinalIgnoreCase));

            if (!wasRenamed) {
                changes.Add(
                    new FileChange(
                        FileChangeType.Added,
                        filePath));
            }
        }

        foreach (IoFileListItem removedItem
                 in removedItems) {
            cancellationToken.ThrowIfCancellationRequested();

            bool wasRenamed =
                changes.Any(
                    change =>
                        change.ChangeType ==
                            FileChangeType.Renamed &&
                        string.Equals(
                            change.PreviousFilePath,
                            removedItem.FilePath,
                            StringComparison.OrdinalIgnoreCase));

            if (!wasRenamed) {
                changes.Add(
                    new FileChange(
                        FileChangeType.Removed,
                        removedItem.FilePath));
            }
        }

        return new FolderDiff(
            changes);
    }

    private async Task DetectRenamesAsync(
        IReadOnlyList<string> addedFiles,
        IReadOnlyList<IoFileListItem> removedItems,
        List<FileChange> changes,
        CancellationToken cancellationToken) {
        if (addedFiles.Count == 0 ||
            removedItems.Count == 0) {
            return;
        }

        Dictionary<string, string> addedFileIdentities =
            await GetFileIdentitiesAsync(
                addedFiles,
                cancellationToken);

        Dictionary<string, IoFileListItem> removedItemsByIdentity =
            removedItems
                .Where(
                    item =>
                        item.ModelIdentity is not null)
                .ToDictionary(
                    item =>
                        item.ModelIdentity!.Value,
                    StringComparer.Ordinal);

        HashSet<string> matchedRemovedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> addedFile
                 in addedFileIdentities) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!removedItemsByIdentity.TryGetValue(
                    addedFile.Value,
                    out IoFileListItem? removedItem)) {
                continue;
            }

            changes.Add(
                new FileChange(
                    FileChangeType.Renamed,
                    addedFile.Key,
                    removedItem.FilePath));

            matchedRemovedPaths.Add(
                removedItem.FilePath);
        }
    }

    private async Task<Dictionary<string, string>>
        GetFileIdentitiesAsync(
            IReadOnlyList<string> filePaths,
            CancellationToken cancellationToken) {
        Dictionary<string, string> identities =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        // Identity lookups use a small amount of controlled parallelism rather
        // than starting one unbounded task per file. This prevents a large
        // folder from creating unnecessary pressure on the file system.
        using SemaphoreSlim semaphore =
            new SemaphoreSlim(8);

        List<Task> tasks =
            new List<Task>();

        foreach (string filePath in filePaths) {
            cancellationToken.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(
                cancellationToken);

            Task task =
                LoadFileIdentityAsync(
                    filePath,
                    identities,
                    semaphore,
                    cancellationToken);

            tasks.Add(
                task);
        }

        await Task.WhenAll(
            tasks);

        return identities;
    }

    private async Task LoadFileIdentityAsync(
        string filePath,
        Dictionary<string, string> identities,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken) {
        try {
            ModelIdentity? identity =
                await Task.Run(
                    () =>
                        fileIdentityProvider.TryGetIdentity(
                            filePath),
                    cancellationToken);

            if (identity is null ||
                cancellationToken.IsCancellationRequested) {
                return;
            }

            lock (identities) {
                identities[filePath] =
                    identity.Value;
            }
        }
        finally {
            semaphore.Release();
        }
    }
}