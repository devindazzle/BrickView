// -----------------------------------------------------------------------------
// MainWindow.Thumbnails.cs
//
// Contains the thumbnail-size handling, viewport loading and metadata loading for BrickView's MainWindow partial class.
// This file is an organizational split only; application behavior is unchanged.
// -----------------------------------------------------------------------------

using System.Windows;

namespace BrickView;

public partial class MainWindow : Window {

    /// <summary>
    /// Applies a newly selected thumbnail size and invalidates existing
    /// thumbnails so they can be regenerated at the new dimensions.
    /// </summary>
    /// <param name="newSize">The thumbnail-size definition to apply.</param>
    private void ThumbnailSizeManager_SizeChanged(
        ThumbnailSizeDefinition newSize) {
        ApplyThumbnailSize(
            newSize);

        foreach (IoFileListItem item
                 in allFileItems) {
            item.InvalidateThumbnail();
        }

        FileList.InvalidateMeasure();
    }

    /// <summary>
    /// Updates the dependency properties that determine thumbnail and card dimensions.
    /// </summary>
    /// <param name="size">The thumbnail-size definition to apply.</param>
    private void ApplyThumbnailSize(
        ThumbnailSizeDefinition size) {
        ThumbnailWidth =
            size.ThumbnailWidth;

        ThumbnailHeight =
            size.ThumbnailHeight;

        CardWidth =
            size.CardWidth;

        CardHeight =
            size.CardHeight;
    }

    /// <summary>
    /// Synchronizes the thumbnail-size radio buttons with the selected preset.
    /// </summary>
    /// <param name="preset">The currently selected thumbnail-size preset.</param>
    private void SetThumbnailSizeSelector(
        ThumbnailSizePreset preset) {
        SmallThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Small;

        MediumThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Medium;

        LargeThumbnailSizeRadioButton.IsChecked =
            preset == ThumbnailSizePreset.Large;
    }

    /// <summary>
    /// Handles a thumbnail-size selection and delegates the actual size change
    /// to the shared thumbnail-size manager.
    /// </summary>
    /// <param name="sender">The radio button that raised the event.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void ThumbnailSizeSelector_Checked(
        object sender,
        RoutedEventArgs e) {
        if (sender is not System.Windows.Controls.RadioButton radioButton) {
            return;
        }

        if (radioButton.Tag is not string presetName) {
            return;
        }

        ThumbnailSizePreset preset;

        switch (presetName) {
            case "Small":

                preset =
                    ThumbnailSizePreset.Small;

                break;

            case "Medium":

                preset =
                    ThumbnailSizePreset.Medium;

                break;

            case "Large":

                preset =
                    ThumbnailSizePreset.Large;

                break;

            default:
                return;
        }

        if (thumbnailSizeManager.Current.Preset ==
            preset) {
            return;
        }

        thumbnailSizeManager.SetSize(
            preset);
    }

    /// <summary>
    /// Loads thumbnails for visible models and preloads thumbnails immediately beyond
    /// the visible viewport to improve scrolling responsiveness.
    /// </summary>
    /// <param name="sender">The virtualized file list that raised the event.</param>
    /// <param name="e">Viewport event data containing the visible item range.</param>
    private void FileList_ViewportChanged(
        object sender,
        RoutedEventArgs e) {
        if (e is not ViewportChangedEventArgs
            viewportEventArgs) {
            return;
        }

        int firstVisibleIndex =
            viewportEventArgs.FirstVisibleIndex;

        int lastVisibleIndex =
            viewportEventArgs.LastVisibleIndex;

        int itemCount =
            FileList.Items.Count;

        if (itemCount == 0) {
            return;
        }

        int clampedFirstIndex =
            Math.Max(
                0,
                Math.Min(
                    firstVisibleIndex,
                    itemCount - 1));

        int clampedLastIndex =
            Math.Max(
                clampedFirstIndex,
                Math.Min(
                    lastVisibleIndex,
                    itemCount - 1));

        for (
            int index = clampedFirstIndex;
            index <= clampedLastIndex;
            index++) {
            if (FileList.Items[index]
                is IoFileListItem item) {

                _ = thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Visible);

                _ = LoadMetadataAsync(
                    item);
            }
        }

        const int preloadCount = 8;

        int preloadStart =
            clampedLastIndex + 1;

        int preloadEnd =
            Math.Min(
                itemCount - 1,
                preloadStart +
                preloadCount -
                1);

        for (
            int index = preloadStart;
            index <= preloadEnd;
            index++) {
            if (FileList.Items[index]
                is IoFileListItem item) {

                _ = thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Preload);
            }
        }
    }

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