// -----------------------------------------------------------------------------
// MainWindow.Search.cs
//
// Contains the Smart Search, Favorite filter and search-result presentation
// for BrickView's MainWindow partial class.
// This file is an organizational split only; application behavior is unchanged.
// -----------------------------------------------------------------------------

using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BrickView;

public partial class MainWindow : Window {

    /// <summary>
    /// Applies the Favorite filter and Smart Search query to the complete model list
    /// and updates the visible ListBox contents and result count.
    /// </summary>
    private void ApplySearchFilter() {
        SmartSearchQuery query =
            currentSearchQuery;

        IEnumerable<IoFileListItem> filteredItems =
            allFileItems;

        if (favoriteFilterEnabled) {
            filteredItems =
                filteredItems.Where(
                    item =>
                        item.IsFavorite);
        }

        filteredItems =
            smartSearchEngine.Search(
                filteredItems,
                query);

        List<IoFileListItem> visibleItems =
            filteredItems.ToList();

        FileList.Items.Clear();

        foreach (IoFileListItem item
                 in visibleItems) {
            FileList.Items.Add(
                item);
        }

        int visibleCount =
            visibleItems.Count;

        int totalCount =
            allFileItems.Count;

        if (visibleCount == totalCount) {
            ModelCountText.Text =
                CreateModelCountText(
                    totalCount);
        }
        else {
            ModelCountText.Text =
                $"{visibleCount} of {totalCount} models";
        }

        bool hasActiveFilter =
            favoriteFilterEnabled ||
            !query.IsEmpty;

        NoResultsText.Visibility =
            hasActiveFilter &&
            visibleItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the Favorite-only filter state from the toolbar button and reapplies the filter.
    /// </summary>
    /// <param name="sender">The Favorite filter button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void FavoriteFilterButton_Click(
        object sender,
        RoutedEventArgs e) {
        favoriteFilterEnabled =
            FavoriteFilterButton.IsChecked == true;

        FavoriteFilterButton.ToolTip =
            favoriteFilterEnabled
                ? "Show all models"
                : "Show favorites only";

        ApplySearchFilter();
    }

    /// <summary>
    /// Determines whether Favorite filtering or a Smart Search query is currently active.
    /// </summary>
    /// <returns><see langword="true"/> when at least one search filter is active.</returns>
    private bool HasActiveSearchFilter() {
        return favoriteFilterEnabled ||
               !currentSearchQuery.IsEmpty;
    }

    /// <summary>
    /// Schedules a deferred search refresh when identity-dependent search state changes,
    /// coalescing multiple requests into a single UI update.
    /// </summary>
    private void RequestSearchRefresh() {
        if (!HasActiveSearchFilter() ||
            searchRefreshPending) {
            return;
        }

        searchRefreshPending =
            true;

        Dispatcher.BeginInvoke(
            new Action(
                () => {
                    searchRefreshPending =
                        false;

                    ApplySearchFilter();
                }),
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Creates the singular or plural result-count text used by the model browser.
    /// </summary>
    /// <param name="modelCount">The number of models.</param>
    /// <returns>A correctly pluralized model-count string.</returns>
    private string CreateModelCountText(
        int modelCount) {
        return modelCount == 1
            ? "1 model"
            : $"{modelCount} models";
    }

    /// <summary>
    /// Parses the current Smart Search text and applies the resulting query immediately.
    /// </summary>
    /// <param name="sender">The search text box that changed.</param>
    /// <param name="e">Text-change event data supplied by WPF.</param>
    private void SearchTextBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e) {
        currentSearchQuery =
            SmartSearchQuery.Parse(
                SearchTextBox.Text);

        ApplySearchFilter();
    }

    /// <summary>
    /// Clears the Smart Search field when the user presses Escape.
    /// </summary>
    /// <param name="sender">The search text box receiving the key event.</param>
    /// <param name="e">Keyboard event data supplied by WPF.</param>
    private void SearchTextBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e) {
        if (e.Key != Key.Escape) {
            return;
        }

        SearchTextBox.Clear();

        SearchTextBox.Focus();

        e.Handled = true;
    }
}