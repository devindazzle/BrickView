// -----------------------------------------------------------------------------
// MainWindow.Tagging.cs
//
// Handles tag-system events that need to update the complete BrickView model
// view.
//
// Global tag deletion is raised by TagPickerControl as a routed UI event. The
// MainWindow class handles that event at the class level so every currently
// loaded IoFileListItem receives a fresh tag snapshot from TagService.
//
// This keeps TagPickerControl independent of MainWindow while ensuring that a
// global tag deletion is reflected immediately on every visible model card.
// -----------------------------------------------------------------------------

using System.Windows;

namespace BrickView;

public partial class MainWindow {
    static MainWindow() {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            TagPickerControl.TagDeletedEvent,
            new RoutedEventHandler(
                MainWindow_TagDeleted));
    }

    private static void MainWindow_TagDeleted(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MainWindow window) {
            return;
        }

        // The global deletion has already been completed by TagService. Refresh
        // every loaded model so all visible cards immediately reflect the new
        // tag state without requiring a folder reload.
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