// -----------------------------------------------------------------------------
// MainWindow.Metadata.cs
//
// Contains metadata-loading orchestration for BrickView's MainWindow partial
// class.
//
// Metadata is loaded on demand when a model becomes visible in the virtualized
// file list. The file-size and last-write-time checks ensure that stale metadata
// is discarded when the underlying model file changes while loading is in
// progress.
//
// This file is an organizational split only; application behavior is unchanged.
// -----------------------------------------------------------------------------

using System.Windows;

namespace BrickView;

public partial class MainWindow : Window {

    /// <summary>
    /// Loads metadata for a model when it has not already been loaded and discards
    /// stale results if the underlying file changed while loading was in progress.
    /// </summary>
    /// <param name="item">The model-list item whose metadata should be loaded.</param>
    /// <returns>A task representing the asynchronous metadata load.</returns>
    private async Task LoadMetadataAsync(
        IoFileListItem item) {
        if (item.Metadata is not null) {
            return;
        }

        try {
            long fileSize =
                item.FileSize;

            DateTime lastWriteTimeUtc =
                item.LastWriteTimeUtc;

            IoModelMetadata? metadata =
                await metadataLoader.LoadAsync(
                    item.FilePath);

            if (item.FileSize != fileSize ||
                item.LastWriteTimeUtc != lastWriteTimeUtc) {
                return;
            }

            item.Metadata =
                metadata;
        }
        catch (Exception) {
            // Metadata loading failures are intentionally ignored here.
            // The existing model remains usable without metadata.
        }
    }
}