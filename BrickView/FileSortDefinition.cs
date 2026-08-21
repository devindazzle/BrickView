// -----------------------------------------------------------------------------
// FileSortDefinition.cs
//
// Defines the sorting options used by BrickView when ordering the model list.
//
// FileSortField identifies the model property used for sorting, while
// FileSortDirection determines whether the result is ascending or descending.
// FileSortDefinition groups these two settings into one immutable value.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Defines the property used to sort the model list.
/// </summary>
public enum FileSortField {
    /// <summary>
    /// Sorts models alphabetically by file name.
    /// </summary>
    FileName,

    /// <summary>
    /// Sorts models by their creation date.
    /// </summary>
    CreatedDate,

    /// <summary>
    /// Sorts models by their last modified date.
    /// </summary>
    ModifiedDate
}

/// <summary>
/// Defines the direction used when sorting the model list.
/// </summary>
public enum FileSortDirection {
    /// <summary>
    /// Sorts from lowest to highest or A to Z.
    /// </summary>
    Ascending,

    /// <summary>
    /// Sorts from highest to lowest or Z to A.
    /// </summary>
    Descending
}

/// <summary>
/// Groups a sort field and sort direction into one sorting definition.
/// </summary>
public sealed class FileSortDefinition {
    /// <summary>
    /// Creates a sorting definition.
    /// </summary>
    /// <param name="field">
    /// The model property used for sorting.
    /// </param>
    /// <param name="direction">
    /// The direction used for sorting.
    /// </param>
    public FileSortDefinition(
        FileSortField field,
        FileSortDirection direction) {
        Field =
            field;

        Direction =
            direction;
    }

    /// <summary>
    /// Gets the property used for sorting.
    /// </summary>
    public FileSortField Field {
        get;
    }

    /// <summary>
    /// Gets the direction used for sorting.
    /// </summary>
    public FileSortDirection Direction {
        get;
    }
}