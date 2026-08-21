// -----------------------------------------------------------------------------
// FolderDiffService.cs
//
// Compares BrickView's existing model items with the files currently present
// in a monitored folder.
//
// The service detects added, removed, modified, unchanged and renamed files.
// Rename detection uses the stable ModelIdentity assigned to IoFileListItem.
//
// File identity lookups are performed asynchronously with controlled
// parallelism so large folders do not create unnecessary pressure on the
// file system or block the UI thread.
// -----------------------------------------------------------------------------

using System.IO;

namespace BrickView;

public sealed class FolderDiffService {
    private readonly WindowsFileIdentityProvider fileIdentityProvider;

    /// <summary>
    /// Creates a folder-diff service using the supplied file identity provider.
    /// </summary>
    /// <param name="fileIdentityProvider">
    /// Provides stable identities used to detect renamed models.
    /// </param>
    public FolderDiffService(
        WindowsFileIdentityProvider fileIdentityProvider) {
        ArgumentNullException.ThrowIfNull(
            fileIdentityProvider);

        this.fileIdentityProvider =
            fileIdentityProvider;
    }

    /// <summary>
    /// Compares the existing model items with the files currently present in
    /// the monitored folder.
    /// </summary>
    /// <param name="existingItems">
    /// The model items currently known by BrickView.
    /// </param>
    /// <param name="currentFiles">
    /// The files currently found in the monitored folder.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the comparison and any outstanding identity lookups.
    /// </param>
    /// <returns>
    /// A FolderDiff containing all detected changes.
    /// </returns>
    public async Task<FolderDiff> CompareAsync(
        IEnumerable<IoFileListItem> existingItems,
        IEnumerable<string> currentFiles,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(
            existingItems);

        ArgumentNullException.ThrowIfNull(
            currentFiles);

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

        // Compare files that currently exist with the model items already
        // known to BrickView.
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
                new FileInfo(
                    filePath);

            bool fileModified =
                fileInfo.Length !=
                    existingItem.FileSize ||
                fileInfo.LastWriteTimeUtc !=
                    existingItem.LastWriteTimeUtc;

            changes.Add(
                new FileChange(
                    fileModified
                        ? FileChangeType.Modified
                        : FileChangeType.Unchanged,
                    filePath));
        }

        List<IoFileListItem> removedItems =
            new List<IoFileListItem>();

        // Find model items whose files are no longer present in the folder.
        foreach (IoFileListItem existingItem
                 in existingItemList) {
            cancellationToken.ThrowIfCancellationRequested();

            if (!currentFileSet.Contains(
                    existingItem.FilePath)) {
                removedItems.Add(
                    existingItem);
            }
        }

        // Added and removed files can represent a rename. Resolve identities
        // before reporting them as independent add/remove operations.
        await DetectRenamesAsync(
            addedFiles,
            removedItems,
            changes,
            cancellationToken);

        // Any added file that was not matched as a rename is a genuine addition.
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

        // Any removed model that was not matched as a rename is a genuine
        // removal.
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

    /// <summary>
    /// Matches newly added files with removed model items by stable model
    /// identity in order to detect renames.
    /// </summary>
    /// <param name="addedFiles">
    /// Files that appeared in the current folder state.
    /// </param>
    /// <param name="removedItems">
    /// Previously known model items whose paths disappeared.
    /// </param>
    /// <param name="changes">
    /// The collection receiving detected rename changes.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the identity resolution process.
    /// </param>
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

        // Match each newly added file against the stable identity of a removed
        // model. A match means the model was renamed rather than replaced.
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
        }
    }

    /// <summary>
    /// Resolves stable model identities for a collection of file paths using
    /// controlled parallelism.
    /// </summary>
    /// <param name="filePaths">
    /// The files whose stable identities should be resolved.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels outstanding identity resolution.
    /// </param>
    /// <returns>
    /// A dictionary mapping file paths to their stable model identities.
    /// </returns>
    private async Task<Dictionary<string, string>>
        GetFileIdentitiesAsync(
            IReadOnlyList<string> filePaths,
            CancellationToken cancellationToken) {
        Dictionary<string, string> identities =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        // Limit the number of simultaneous file-system identity lookups so
        // large folders do not create an unbounded number of tasks.
        using SemaphoreSlim semaphore =
            new SemaphoreSlim(8);

        List<Task> tasks =
            new List<Task>();

        foreach (string filePath in filePaths) {
            cancellationToken.ThrowIfCancellationRequested();

            await semaphore.WaitAsync(
                cancellationToken);

            tasks.Add(
                LoadFileIdentityAsync(
                    filePath,
                    identities,
                    semaphore,
                    cancellationToken));
        }

        await Task.WhenAll(
            tasks);

        return identities;
    }

    /// <summary>
    /// Resolves one file's stable model identity and adds the result to the
    /// shared identity collection.
    /// </summary>
    /// <param name="filePath">
    /// The file whose identity should be resolved.
    /// </param>
    /// <param name="identities">
    /// The shared identity collection populated by the parallel operations.
    /// </param>
    /// <param name="semaphore">
    /// Controls the number of concurrent identity lookups.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the identity lookup.
    /// </param>
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