// -----------------------------------------------------------------------------
// ApplicationState.cs
//
// Defines the persistent application-level state used by BrickView.
//
// ApplicationState contains only UI/session preferences that belong to the
// application itself, such as the main window position, the last selected
// folder, thumbnail size and sorting preferences.
//
// Model-specific data such as tags and favorites is intentionally not stored
// here. Those belong to the individual models and are persisted separately.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class ApplicationState {
    /// <summary>
    /// Gets or sets the last horizontal position of the main window.
    /// </summary>
    public double? WindowLeft {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the last vertical position of the main window.
    /// </summary>
    public double? WindowTop {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the last width of the main window.
    /// </summary>
    public double? WindowWidth {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the last height of the main window.
    /// </summary>
    public double? WindowHeight {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the folder that was selected when BrickView was last closed.
    /// </summary>
    public string? LastSelectedFolder {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the thumbnail size preset used by the model browser.
    /// </summary>
    public ThumbnailSizePreset? ThumbnailSizePreset {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the field currently used to sort the model list.
    /// </summary>
    public FileSortField SortField {
        get;
        set;
    } =
        FileSortField.FileName;

    /// <summary>
    /// Gets or sets the direction currently used to sort the model list.
    /// </summary>
    public FileSortDirection SortDirection {
        get;
        set;
    } =
        FileSortDirection.Ascending;
}