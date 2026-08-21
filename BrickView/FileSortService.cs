// -----------------------------------------------------------------------------
// FileSortService.cs
//
// Provides sorting for BrickView's model list.
//
// The service keeps sorting logic outside the UI controller so callers only
// need to provide the models and a FileSortDefinition. It supports sorting by
// file name, creation date and modification date in ascending or descending
// order.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class FileSortService {
    /// <summary>
    /// Sorts the supplied model items according to the specified definition.
    /// </summary>
    /// <param name="items">
    /// The model items to sort.
    /// </param>
    /// <param name="sortDefinition">
    /// Defines both the field and direction used for sorting.
    /// </param>
    /// <returns>
    /// A new list containing the sorted model items.
    /// </returns>
    public List<IoFileListItem> Sort(
        IEnumerable<IoFileListItem> items,
        FileSortDefinition sortDefinition) {
        ArgumentNullException.ThrowIfNull(
            items);

        ArgumentNullException.ThrowIfNull(
            sortDefinition);

        IEnumerable<IoFileListItem> sortedItems =
            sortDefinition.Field switch {
                FileSortField.FileName =>
                    items.OrderBy(
                        item => item.FileName,
                        StringComparer.OrdinalIgnoreCase),

                FileSortField.CreatedDate =>
                    items.OrderBy(
                        item => item.CreationTimeUtc),

                FileSortField.ModifiedDate =>
                    items.OrderBy(
                        item => item.LastWriteTimeUtc),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(sortDefinition),
                        sortDefinition,
                        "Unknown file sort field.")
            };

        if (sortDefinition.Direction ==
            FileSortDirection.Descending) {
            sortedItems =
                sortedItems.Reverse();
        }

        return sortedItems.ToList();
    }
}