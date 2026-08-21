// -----------------------------------------------------------------------------
// MainWindow.Tagging.cs
//
// Contains the Favorite and tag interaction handlers for BrickView's MainWindow partial class.
// This file is an organizational split only; application behavior is unchanged.
// -----------------------------------------------------------------------------

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BrickView;

public partial class MainWindow : Window {

    /// <summary>
    /// Toggles the Favorite state of the model represented by the clicked indicator
    /// and refreshes the list when the Favorite filter is active.
    /// </summary>
    /// <param name="sender">The Favorite indicator that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
    private void FavoriteIndicator_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e) {
        if (sender is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        if (item.ModelIdentity is null) {
            return;
        }

        bool newFavoriteState =
            !item.IsFavorite;

        bool changed =
            tagService.SetFavorite(
                item.ModelIdentity,
                newFavoriteState);

        if (changed) {
            item.IsFavorite =
                newFavoriteState;
        }

        e.Handled = true;

        if (favoriteFilterEnabled) {
            ApplySearchFilter();
        }
    }

    /// <summary>
    /// Opens the shared tag picker for the model represented by the clicked Add Tag button.
    /// </summary>
    /// <param name="sender">The Add Tag button that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
    private void AddTagButton_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Button button) {
            return;
        }

        if (button.DataContext
            is not IoFileListItem item) {
            return;
        }

        TagPicker.OpenFor(
            button,
            item);

        e.Handled = true;
    }

    /// <summary>
    /// Removes the selected tag from the model represented by the clicked tag button
    /// and refreshes the active search when necessary.
    /// </summary>
    /// <param name="sender">The tag remove button that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
    private void RemoveTag_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Button button) {
            return;
        }

        if (button.DataContext
            is not TagDefinition tag) {
            return;
        }

        if (button.Tag is not IoFileListItem item) {
            return;
        }

        if (item.ModelIdentity is null) {
            return;
        }

        bool removed =
            tagService.RemoveTag(
                item.ModelIdentity,
                tag.Name);

        if (removed) {
            item.SetTags(
                tagService.GetTags(
                    item.ModelIdentity));

            if (!currentSearchQuery.IsEmpty) {
                RequestSearchRefresh();
            }
        }

        e.Handled = true;
    }

}