// -----------------------------------------------------------------------------
// MainWindow.Sorting.cs
//
// Contains the sorting state, sorting operations and sort-menu presentation for
// BrickView's MainWindow partial class.
//
// FileSortField is used directly as the MainWindow sort state so the same enum
// represents both the active UI sort field and the persisted application state.
// This removes the previous duplicate SortField representation and its
// conversion logic.
//
// This file is an organizational split only; application behavior is unchanged.
// -----------------------------------------------------------------------------

using System.Windows;

namespace BrickView;

public partial class MainWindow : Window {

    /// <summary>
    /// Selects file name as the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortFileName_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            FileSortField.FileName);
    }

    /// <summary>
    /// Selects creation date as the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortCreatedDate_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            FileSortField.CreatedDate);
    }

    /// <summary>
    /// Selects last-modified date as the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortModifiedDate_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortField(
            FileSortField.ModifiedDate);
    }

    /// <summary>
    /// Selects ascending order for the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortAscending_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortDirection(
            FileSortDirection.Ascending);
    }

    /// <summary>
    /// Selects descending order for the active sort field.
    /// </summary>
    /// <param name="sender">The sort-menu button that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void SortDescending_Click(
        object sender,
        RoutedEventArgs e) {
        SetSortDirection(
            FileSortDirection.Descending);
    }

    /// <summary>
    /// Changes the active sort field and reapplies sorting, filtering and menu state.
    /// </summary>
    /// <param name="sortField">The field to use for sorting.</param>
    private void SetSortField(
        FileSortField sortField) {
        if (currentSortField ==
            sortField) {
            return;
        }

        currentSortField =
            sortField;

        SortAllFileItems();

        ApplySearchFilter();

        UpdateSortMenu();
    }

    /// <summary>
    /// Changes the active sort direction and reapplies sorting, filtering and menu state.
    /// </summary>
    /// <param name="sortDirection">The direction to use for sorting.</param>
    private void SetSortDirection(
        FileSortDirection sortDirection) {
        if (currentSortDirection ==
            sortDirection) {
            return;
        }

        currentSortDirection =
            sortDirection;

        SortAllFileItems();

        ApplySearchFilter();

        UpdateSortMenu();
    }

    /// <summary>
    /// Sorts the complete in-memory model list using the selected field and direction.
    /// </summary>
    private void SortAllFileItems() {
        switch (currentSortField) {
            case FileSortField.FileName:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        StringComparer.OrdinalIgnoreCase.Compare(
                            left.FileName,
                            right.FileName));

                break;

            case FileSortField.CreatedDate:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        DateTime.Compare(
                            left.CreationTimeUtc,
                            right.CreationTimeUtc));

                break;

            case FileSortField.ModifiedDate:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        DateTime.Compare(
                            left.LastWriteTimeUtc,
                            right.LastWriteTimeUtc));

                break;
        }

        if (currentSortDirection ==
            FileSortDirection.Descending) {
            allFileItems.Reverse();
        }
    }

    /// <summary>
    /// Updates the sort popup controls and toolbar labels to reflect the current sort state.
    /// </summary>
    private void UpdateSortMenu() {
        FileNameSortCheck.Visibility =
            currentSortField ==
            FileSortField.FileName
                ? Visibility.Visible
                : Visibility.Collapsed;

        CreatedDateSortCheck.Visibility =
            currentSortField ==
            FileSortField.CreatedDate
                ? Visibility.Visible
                : Visibility.Collapsed;

        ModifiedDateSortCheck.Visibility =
            currentSortField ==
            FileSortField.ModifiedDate
                ? Visibility.Visible
                : Visibility.Collapsed;

        AscendingSortCheck.Visibility =
            currentSortDirection ==
            FileSortDirection.Ascending
                ? Visibility.Visible
                : Visibility.Collapsed;

        DescendingSortCheck.Visibility =
            currentSortDirection ==
            FileSortDirection.Descending
                ? Visibility.Visible
                : Visibility.Collapsed;

        SortDirectionText.Text =
            currentSortDirection ==
            FileSortDirection.Ascending
                ? "↑"
                : "↓";

        switch (currentSortField) {
            case FileSortField.FileName:

                SortButtonContent.Text =
                    "File name";

                break;

            case FileSortField.CreatedDate:

                SortButtonContent.Text =
                    "Created date";

                break;

            case FileSortField.ModifiedDate:

                SortButtonContent.Text =
                    "Modified date";

                break;
        }
    }
}