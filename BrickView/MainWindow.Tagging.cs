// -----------------------------------------------------------------------------
// MainWindow.Tagging.cs
//
// Handles tag-system events that require the complete BrickView model view to
// be refreshed.
//
// Global tag deletion is raised by TagPickerControl as a routed UI event.
// MainWindow handles that event at the class level so all currently loaded
// IoFileListItem instances receive a fresh tag snapshot from TagService.
//
// This keeps TagPickerControl independent of MainWindow while ensuring that a
// global tag deletion is reflected immediately on every loaded model card.
// -----------------------------------------------------------------------------

using System.Windows;

namespace BrickView;

public partial class MainWindow {
    /// <summary>
    /// Registers MainWindow's class-level handler for global tag deletion.
    /// </summary>
    static MainWindow() {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            TagPickerControl.TagDeletedEvent,
            new RoutedEventHandler(
                MainWindow_TagDeleted));
    }

    /// <summary>
    /// Refreshes the tag snapshots of all loaded models after a global tag has
    /// been deleted.
    /// </summary>
    /// <param name="sender">
    /// The MainWindow receiving the routed event.
    /// </param>
    /// <param name="e">
    /// The routed tag-deletion event.
    /// </param>
    private static void MainWindow_TagDeleted(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MainWindow window) {
            return;
        }

        // TagService has already removed the tag globally. Refresh every
        // loaded model so visible cards immediately reflect the new state
        // without requiring a folder reload.
        foreach (IoFileListItem item
                 in window.allFileItems) {

            if (item.ModelIdentity is null) {
                continue;
            }

            item.SetTags(
                window.tagService.GetTags(
                    item.ModelIdentity));
        }

        e.Handled =
            true;
    }
}