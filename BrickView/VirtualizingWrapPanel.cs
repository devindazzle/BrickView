// -----------------------------------------------------------------------------
// VirtualizingWrapPanel.cs
//
// Provides the virtualized wrapping layout used by BrickView for displaying
// model cards efficiently in a scrollable grid.
//
// Responsibilities:
// - Calculates the number of columns and rows required by the current layout.
// - Calculates the scrollable extent and current viewport dimensions.
// - Generates only the item containers that are currently visible.
// - Removes item containers that have moved outside the visible viewport.
// - Arranges realized item containers in a wrapping grid.
// - Implements WPF's IScrollInfo interface for scrolling support.
// - Reports changes to the visible item range through ViewportChanged.
// - Reacts to changes in the global thumbnail size definition.
//
// The panel uses ThumbnailSizeManager to obtain the dimensions of virtualized
// items. It therefore does not define thumbnail dimensions itself.
//
// Virtualization is implemented through WPF's IItemContainerGenerator. Only
// items inside the current visible row range are realized and measured.
//
// This class contains layout and virtualization infrastructure only. Model
// data, thumbnail loading and application-level item management are handled by
// other BrickView components.
// -----------------------------------------------------------------------------

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace BrickView;

/// <summary>
/// Provides a virtualized wrapping panel for efficiently displaying BrickView
/// model items in a scrollable grid.
/// </summary>
/// <remarks>
/// The panel calculates a regular grid based on the current item dimensions and
/// the available width. Only items intersecting the current vertical viewport
/// are realized through WPF's item-container generator.
///
/// The panel also implements <see cref="IScrollInfo"/> so it can act as the
/// scrolling surface for a containing <see cref="ScrollViewer"/>.
/// </remarks>
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo {
    private readonly ThumbnailSizeManager thumbnailSizeManager;

    private double itemWidth;

    private double itemHeight;

    private Size extent = new Size(0, 0);

    private Size viewport = new Size(0, 0);

    private double horizontalOffset;

    private double verticalOffset;

    private int columnCount = 1;

    private int lastReportedFirstVisibleIndex = -1;

    private int lastReportedLastVisibleIndex = -1;

    private ScrollViewer? scrollOwner;

    /// <summary>
    /// Identifies the routed event raised when the visible item range changes.
    /// </summary>
    public static readonly RoutedEvent ViewportChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ViewportChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(VirtualizingWrapPanel));

    /// <summary>
    /// Initializes the panel and obtains the current item dimensions from the
    /// shared thumbnail-size manager.
    /// </summary>
    public VirtualizingWrapPanel() {
        thumbnailSizeManager =
            ThumbnailSizeManager.Instance;

        itemWidth =
            thumbnailSizeManager.Current.ItemWidth;

        itemHeight =
            thumbnailSizeManager.Current.ItemHeight;

        // Layout dimensions must be refreshed whenever the application-wide
        // thumbnail size changes.
        thumbnailSizeManager.SizeChanged +=
            ThumbnailSizeManager_SizeChanged;
    }

    /// <summary>
    /// Occurs when the first or last visible item index changes.
    /// </summary>
    /// <remarks>
    /// The event bubbles through the WPF visual tree and carries the visible
    /// range through <see cref="ViewportChangedEventArgs"/>.
    /// </remarks>
    public event RoutedEventHandler ViewportChanged {
        add {
            AddHandler(
                ViewportChangedEvent,
                value);
        }

        remove {
            RemoveHandler(
                ViewportChangedEvent,
                value);
        }
    }

    /// <summary>
    /// Gets the <see cref="ItemsControl"/> that owns this panel.
    /// </summary>
    /// <returns>
    /// The owning items control, or <see langword="null"/> when the panel is
    /// not currently associated with an items control.
    /// </returns>
    private ItemsControl? ItemsOwner {
        get {
            return ItemsControl.GetItemsOwner(
                this);
        }
    }

    /// <summary>
    /// Measures the panel, calculates its scrollable extent and realizes the
    /// item containers required for the current viewport.
    /// </summary>
    /// <param name="availableSize">
    /// The space available to the panel from its parent.
    /// </param>
    /// <returns>
    /// The size requested by the panel.
    /// </returns>
    protected override Size MeasureOverride(
        Size availableSize) {
        ItemsControl? itemsOwner =
            ItemsOwner;

        if (itemsOwner is null) {
            return availableSize;
        }

        int itemCount =
            itemsOwner.Items.Count;

        if (itemCount == 0) {
            extent =
                new Size(
                    availableSize.Width,
                    0);

            viewport =
                availableSize;

            scrollOwner?.InvalidateScrollInfo();

            return availableSize;
        }

        double availableWidth =
            availableSize.Width;

        if (double.IsInfinity(
                availableWidth) ||
            availableWidth <= 0) {
            availableWidth =
                itemWidth;
        }

        // The number of columns is determined by how many complete item widths
        // fit into the available horizontal space.
        columnCount =
            Math.Max(
                1,
                (int)Math.Floor(
                    availableWidth /
                    itemWidth));

        int rowCount =
            (int)Math.Ceiling(
                (double)itemCount /
                columnCount);

        double extentHeight =
            rowCount *
            itemHeight;

        viewport =
            availableSize;

        extent =
            new Size(
                availableWidth,
                extentHeight);

        // A layout change can reduce the maximum valid vertical offset. Clamp
        // the existing offset before calculating the visible item range.
        verticalOffset =
            ClampVerticalOffset(
                verticalOffset);

        GenerateVisibleItems(
            itemCount,
            availableSize);

        scrollOwner?.InvalidateScrollInfo();

        return availableSize;
    }

    /// <summary>
    /// Arranges all currently realized item containers at their calculated
    /// grid positions.
    /// </summary>
    /// <param name="finalSize">
    /// The final size allocated to the panel.
    /// </param>
    /// <returns>
    /// The final size used by the panel.
    /// </returns>
    protected override Size ArrangeOverride(
        Size finalSize) {
        IItemContainerGenerator generator =
            ItemContainerGenerator;

        for (
            int i = 0;
            i < InternalChildren.Count;
            i++) {
            UIElement child =
                InternalChildren[i];

            GeneratorPosition position =
                new GeneratorPosition(
                    i,
                    0);

            int itemIndex =
                generator.IndexFromGeneratorPosition(
                    position);

            if (itemIndex < 0) {
                continue;
            }

            int row =
                itemIndex /
                columnCount;

            int column =
                itemIndex %
                columnCount;

            double x =
                column *
                itemWidth;

            double y =
                row *
                itemHeight -
                verticalOffset;

            child.Arrange(
                new Rect(
                    x,
                    y,
                    itemWidth,
                    itemHeight));
        }

        return finalSize;
    }

    /// <summary>
    /// Generates and measures the item containers required for the current
    /// visible viewport and removes containers that are no longer visible.
    /// </summary>
    /// <param name="itemCount">
    /// The total number of items in the owning items control.
    /// </param>
    /// <param name="availableSize">
    /// The size currently available to the panel.
    /// </param>
    private void GenerateVisibleItems(
        int itemCount,
        Size availableSize) {
        if (itemCount == 0) {
            return;
        }

        double viewportBottom =
            verticalOffset +
            availableSize.Height;

        int firstVisibleRow =
            Math.Max(
                0,
                (int)Math.Floor(
                    verticalOffset /
                    itemHeight));

        int lastVisibleRow =
            Math.Min(
                (int)Math.Ceiling(
                    viewportBottom /
                    itemHeight),
                (int)Math.Ceiling(
                    (double)itemCount /
                    columnCount) -
                1);

        int firstVisibleIndex =
            firstVisibleRow *
            columnCount;

        int lastVisibleIndex =
            Math.Min(
                itemCount - 1,
                ((lastVisibleRow + 1) *
                 columnCount) -
                1);

        IItemContainerGenerator generator =
            ItemContainerGenerator;

        GeneratorPosition startPosition =
            generator.GeneratorPositionFromIndex(
                firstVisibleIndex);

        // GeneratorPosition.Offset indicates whether the requested index
        // points directly at an existing generated container or between
        // generated containers.
        int childIndex =
            startPosition.Offset == 0
                ? startPosition.Index
                : startPosition.Index + 1;

        using (
            generator.StartAt(
                startPosition,
                GeneratorDirection.Forward,
                true)) {
            for (
                int itemIndex =
                    firstVisibleIndex;
                itemIndex <= lastVisibleIndex;
                itemIndex++) {
                UIElement? child =
                    generator.GenerateNext(
                        out bool newlyRealized)
                    as UIElement;

                if (child is null) {
                    continue;
                }

                if (newlyRealized) {
                    // Newly generated containers must be inserted into the
                    // panel's internal visual collection at the position
                    // corresponding to the generator.
                    if (childIndex >=
                        InternalChildren.Count) {
                        AddInternalChild(
                            child);
                    }
                    else {
                        InsertInternalChild(
                            childIndex,
                            child);
                    }

                    generator.PrepareItemContainer(
                        child);
                }

                child.Measure(
                    new Size(
                        itemWidth,
                        itemHeight));

                childIndex++;
            }
        }

        // Remove realized containers that are no longer part of the current
        // visible range so virtualization can release their visual resources.
        CleanupItems(
            firstVisibleIndex,
            lastVisibleIndex);

        // Notify listeners only when the visible range has actually changed.
        if (
            firstVisibleIndex !=
                lastReportedFirstVisibleIndex ||
            lastVisibleIndex !=
                lastReportedLastVisibleIndex) {
            lastReportedFirstVisibleIndex =
                firstVisibleIndex;

            lastReportedLastVisibleIndex =
                lastVisibleIndex;

            RaiseEvent(
                new ViewportChangedEventArgs(
                    ViewportChangedEvent,
                    firstVisibleIndex,
                    lastVisibleIndex));
        }
    }

    /// <summary>
    /// Removes realized item containers that fall outside the specified
    /// visible item-index range.
    /// </summary>
    /// <param name="firstVisibleIndex">
    /// The index of the first item that should remain realized.
    /// </param>
    /// <param name="lastVisibleIndex">
    /// The index of the last item that should remain realized.
    /// </param>
    private void CleanupItems(
        int firstVisibleIndex,
        int lastVisibleIndex) {
        IItemContainerGenerator generator =
            ItemContainerGenerator;

        // Iterate backwards because removing an internal child changes the
        // indexes of the remaining children.
        for (
            int i =
                InternalChildren.Count - 1;
            i >= 0;
            i--) {
            GeneratorPosition position =
                new GeneratorPosition(
                    i,
                    0);

            int itemIndex =
                generator.IndexFromGeneratorPosition(
                    position);

            if (
                itemIndex <
                    firstVisibleIndex ||
                itemIndex >
                    lastVisibleIndex) {
                generator.Remove(
                    position,
                    1);

                RemoveInternalChildRange(
                    i,
                    1);
            }
        }
    }

    /// <summary>
    /// Updates the item dimensions when the shared thumbnail size changes and
    /// requests a new layout pass.
    /// </summary>
    /// <param name="newSize">
    /// The newly selected thumbnail size definition.
    /// </param>
    private void ThumbnailSizeManager_SizeChanged(
        ThumbnailSizeDefinition newSize) {
        itemWidth =
            newSize.ItemWidth;

        itemHeight =
            newSize.ItemHeight;

        // Force the visible-range notification to be emitted again because
        // the layout geometry has changed even if the item indexes have not.
        lastReportedFirstVisibleIndex =
            -1;

        lastReportedLastVisibleIndex =
            -1;

        InvalidateMeasure();

        scrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>
    /// Responds to changes in the items collection by invalidating the current
    /// layout and visible-range state.
    /// </summary>
    /// <param name="sender">
    /// The object that raised the items-changed notification.
    /// </param>
    /// <param name="args">
    /// Information describing the change to the items collection.
    /// </param>
    protected override void OnItemsChanged(
        object sender,
        ItemsChangedEventArgs args) {
        // The previous visible range may no longer represent the same items
        // after an insertion, removal or other collection change.
        lastReportedFirstVisibleIndex = -1;

        lastReportedLastVisibleIndex = -1;

        InvalidateMeasure();

        base.OnItemsChanged(
            sender,
            args);
    }

    #region IScrollInfo

    /// <summary>
    /// Gets or sets a value indicating whether horizontal scrolling is enabled.
    /// </summary>
    public bool CanHorizontallyScroll {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether vertical scrolling is enabled.
    /// </summary>
    public bool CanVerticallyScroll {
        get;
        set;
    }

    /// <summary>
    /// Gets the total height of the scrollable content.
    /// </summary>
    public double ExtentHeight {
        get {
            return extent.Height;
        }
    }

    /// <summary>
    /// Gets the total width of the scrollable content.
    /// </summary>
    public double ExtentWidth {
        get {
            return extent.Width;
        }
    }

    /// <summary>
    /// Gets the current horizontal scroll offset.
    /// </summary>
    public double HorizontalOffset {
        get {
            return horizontalOffset;
        }
    }

    /// <summary>
    /// Gets the current vertical scroll offset.
    /// </summary>
    public double VerticalOffset {
        get {
            return verticalOffset;
        }
    }

    /// <summary>
    /// Gets the height of the currently visible viewport.
    /// </summary>
    public double ViewportHeight {
        get {
            return viewport.Height;
        }
    }

    /// <summary>
    /// Gets the width of the currently visible viewport.
    /// </summary>
    public double ViewportWidth {
        get {
            return viewport.Width;
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="ScrollViewer"/> that owns this scrolling
    /// information.
    /// </summary>
    public ScrollViewer? ScrollOwner {
        get {
            return scrollOwner;
        }

        set {
            scrollOwner = value;
        }
    }

    /// <summary>
    /// Scrolls the content down by one item height.
    /// </summary>
    public void LineDown() {
        SetVerticalOffset(
            verticalOffset +
            itemHeight);
    }

    /// <summary>
    /// Scrolls the content up by one item height.
    /// </summary>
    public void LineUp() {
        SetVerticalOffset(
            verticalOffset -
            itemHeight);
    }

    /// <summary>
    /// Scrolls the content left by one item width.
    /// </summary>
    public void LineLeft() {
        SetHorizontalOffset(
            horizontalOffset -
            itemWidth);
    }

    /// <summary>
    /// Scrolls the content right by one item width.
    /// </summary>
    public void LineRight() {
        SetHorizontalOffset(
            horizontalOffset +
            itemWidth);
    }

    /// <summary>
    /// Scrolls the content down by one item height in response to a mouse-wheel
    /// operation.
    /// </summary>
    public void MouseWheelDown() {
        SetVerticalOffset(
            verticalOffset +
            itemHeight);
    }

    /// <summary>
    /// Scrolls the content up by one item height in response to a mouse-wheel
    /// operation.
    /// </summary>
    public void MouseWheelUp() {
        SetVerticalOffset(
            verticalOffset -
            itemHeight);
    }

    /// <summary>
    /// Scrolls the content left by one item width in response to a mouse-wheel
    /// operation.
    /// </summary>
    public void MouseWheelLeft() {
        SetHorizontalOffset(
            horizontalOffset -
            itemWidth);
    }

    /// <summary>
    /// Scrolls the content right by one item width in response to a mouse-wheel
    /// operation.
    /// </summary>
    public void MouseWheelRight() {
        SetHorizontalOffset(
            horizontalOffset +
            itemWidth);
    }

    /// <summary>
    /// Scrolls the content down by one viewport height.
    /// </summary>
    public void PageDown() {
        SetVerticalOffset(
            verticalOffset +
            viewport.Height);
    }

    /// <summary>
    /// Scrolls the content up by one viewport height.
    /// </summary>
    public void PageUp() {
        SetVerticalOffset(
            verticalOffset -
            viewport.Height);
    }

    /// <summary>
    /// Scrolls the content left by one viewport width.
    /// </summary>
    public void PageLeft() {
        SetHorizontalOffset(
            horizontalOffset -
            viewport.Width);
    }

    /// <summary>
    /// Scrolls the content right by one viewport width.
    /// </summary>
    public void PageRight() {
        SetHorizontalOffset(
            horizontalOffset +
            viewport.Width);
    }

    /// <summary>
    /// Sets the horizontal scroll offset while respecting the available
    /// horizontal scrolling range.
    /// </summary>
    /// <param name="offset">
    /// The requested horizontal offset.
    /// </param>
    public void SetHorizontalOffset(
        double offset) {
        if (!CanHorizontallyScroll) {
            return;
        }

        double newOffset =
            Math.Max(
                0,
                Math.Min(
                    offset,
                    Math.Max(
                        0,
                        ExtentWidth -
                        ViewportWidth)));

        // Ignore insignificant changes to avoid unnecessary layout passes.
        if (
            Math.Abs(
                newOffset -
                horizontalOffset)
            < 0.1) {
            return;
        }

        horizontalOffset =
            newOffset;

        InvalidateMeasure();

        scrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>
    /// Sets the vertical scroll offset while respecting the available
    /// vertical scrolling range.
    /// </summary>
    /// <param name="offset">
    /// The requested vertical offset.
    /// </param>
    public void SetVerticalOffset(
        double offset) {
        if (!CanVerticallyScroll) {
            return;
        }

        double newOffset =
            ClampVerticalOffset(
                offset);

        // Ignore insignificant changes to avoid unnecessary layout passes.
        if (
            Math.Abs(
                newOffset -
                verticalOffset)
            < 0.1) {
            return;
        }

        verticalOffset =
            newOffset;

        InvalidateMeasure();

        scrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>
    /// Ensures that the specified visual rectangle is visible within the
    /// current vertical viewport.
    /// </summary>
    /// <param name="visual">
    /// The visual containing the rectangle.
    /// </param>
    /// <param name="rectangle">
    /// The rectangle that should be made visible.
    /// </param>
    /// <returns>
    /// The supplied rectangle.
    /// </returns>
    /// <remarks>
    /// The current implementation adjusts only the vertical offset because the
    /// panel's visible-item generation is based on vertical scrolling.
    /// </remarks>
    public Rect MakeVisible(
        Visual visual,
        Rect rectangle) {
        if (visual is null) {
            return rectangle;
        }

        if (rectangle.Top < 0) {
            SetVerticalOffset(
                verticalOffset +
                rectangle.Top);
        }
        else if (
            rectangle.Bottom >
            ViewportHeight) {
            SetVerticalOffset(
                verticalOffset +
                rectangle.Bottom -
                ViewportHeight);
        }

        return rectangle;
    }

    /// <summary>
    /// Restricts a vertical scroll offset to the valid range between the top
    /// and bottom of the scrollable extent.
    /// </summary>
    /// <param name="offset">
    /// The requested vertical offset.
    /// </param>
    /// <returns>
    /// A vertical offset within the valid scrolling range.
    /// </returns>
    private double ClampVerticalOffset(
        double offset) {
        return Math.Max(
            0,
            Math.Min(
                offset,
                Math.Max(
                    0,
                    ExtentHeight -
                    ViewportHeight)));
    }

    #endregion
}