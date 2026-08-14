namespace BrickView;

public sealed class FileSortService {
    public List<IoFileListItem> Sort(
        IEnumerable<IoFileListItem> items,
        FileSortDefinition sortDefinition) {
        IEnumerable<IoFileListItem> sortedItems;

        switch (sortDefinition.Field) {
            case FileSortField.FileName:

                sortedItems =
                    items.OrderBy(
                        item => item.FileName,
                        StringComparer.OrdinalIgnoreCase);

                break;

            case FileSortField.CreatedDate:

                sortedItems =
                    items.OrderBy(
                        item => item.CreationTimeUtc);

                break;

            case FileSortField.ModifiedDate:

                sortedItems =
                    items.OrderBy(
                        item => item.LastWriteTimeUtc);

                break;

            default:

                throw new ArgumentOutOfRangeException(
                    nameof(sortDefinition),
                    sortDefinition,
                    "Unknown file sort field.");
        }

        if (sortDefinition.Direction ==
            FileSortDirection.Descending) {
            sortedItems =
                sortedItems.Reverse();
        }

        return sortedItems.ToList();
    }
}