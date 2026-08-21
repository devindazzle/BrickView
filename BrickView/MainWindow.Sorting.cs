// -----------------------------------------------------------------------------
// MainWindow.Sorting.cs
//
// Contains the sorting state, sorting operations and sort-menu presentation for BrickView's MainWindow partial class.
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
            SortField.FileName);
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
            SortField.CreatedDate);
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
            SortField.ModifiedDate);
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
        SortField sortField) {
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
            case SortField.FileName:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        StringComparer.OrdinalIgnoreCase.Compare(
                            left.FileName,
                            right.FileName));

                break;

            case SortField.CreatedDate:

                allFileItems.Sort(
                    (
                        left,
                        right) =>
                        DateTime.Compare(
                            left.CreationTimeUtc,
                            right.CreationTimeUtc));

                break;

            case SortField.ModifiedDate:

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
            SortField.FileName
                ? Visibility.Visible
                : Visibility.Collapsed;

        CreatedDateSortCheck.Visibility =
            currentSortField ==
            SortField.CreatedDate
                ? Visibility.Visible
                : Visibility.Collapsed;

        ModifiedDateSortCheck.Visibility =
            currentSortField ==
            SortField.ModifiedDate
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
            case SortField.FileName:

                SortButtonContent.Text =
                    "File name";

                break;

            case SortField.CreatedDate:

                SortButtonContent.Text =
                    "Created date";

                break;

            case SortField.ModifiedDate:

                SortButtonContent.Text =
                    "Modified date";

                break;
        }
    }

    /// <summary>
    /// Converts the window's internal sort-field representation to the persisted
    /// application-state representation.
    /// </summary>
    /// <returns>The corresponding persisted file-sort field.</returns>
    private FileSortField GetFileSortField() {
        switch (currentSortField) {
            case SortField.FileName:

                return FileSortField.FileName;

            case SortField.CreatedDate:

                return FileSortField.CreatedDate;

            case SortField.ModifiedDate:

                return FileSortField.ModifiedDate;

            default:

                return FileSortField.FileName;
        }
    }

}