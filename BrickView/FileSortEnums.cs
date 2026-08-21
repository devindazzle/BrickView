// -----------------------------------------------------------------------------
// FileSortEnums.cs
//
// Defines the sorting options used by BrickView when ordering the model list.
//
// FileSortField identifies the model property used for sorting, while
// FileSortDirection determines whether the result is ascending or descending.
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