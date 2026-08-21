// -----------------------------------------------------------------------------
// ViewportChangedEventArgs.cs
//
// Provides the event data used when BrickView's visible viewport range changes.
//
// Responsibilities:
// - Carries the index of the first visible model item.
// - Carries the index of the last visible model item.
// - Integrates the viewport information with WPF's routed-event system.
//
// The class contains only event data. Determining which items are visible and
// raising the corresponding event are responsibilities of the viewport/UI
// infrastructure.
// -----------------------------------------------------------------------------

using System.Windows;

namespace BrickView;

/// <summary>
/// Provides event data for a change in the visible model-item viewport.
/// </summary>
public class ViewportChangedEventArgs : RoutedEventArgs {
    /// <summary>
    /// Gets the zero-based index of the first visible model item.
    /// </summary>
    public int FirstVisibleIndex {
        get;
    }

    /// <summary>
    /// Gets the zero-based index of the last visible model item.
    /// </summary>
    public int LastVisibleIndex {
        get;
    }

    /// <summary>
    /// Initializes event data for a viewport change.
    /// </summary>
    /// <param name="routedEvent">
    /// The routed event associated with this event data.
    /// </param>
    /// <param name="firstVisibleIndex">
    /// The zero-based index of the first visible model item.
    /// </param>
    /// <param name="lastVisibleIndex">
    /// The zero-based index of the last visible model item.
    /// </param>
    public ViewportChangedEventArgs(
        RoutedEvent routedEvent,
        int firstVisibleIndex,
        int lastVisibleIndex)
        : base(
            routedEvent) {
        FirstVisibleIndex =
            firstVisibleIndex;

        LastVisibleIndex =
            lastVisibleIndex;
    }
}