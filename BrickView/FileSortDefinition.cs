namespace BrickView;

public enum FileSortField {
    FileName,
    CreatedDate,
    ModifiedDate
}

public enum FileSortDirection {
    Ascending,
    Descending
}

public sealed class FileSortDefinition {
    public FileSortDefinition(
        FileSortField field,
        FileSortDirection direction) {
        Field = field;
        Direction = direction;
    }

    public FileSortField Field {
        get;
    }

    public FileSortDirection Direction {
        get;
    }
}