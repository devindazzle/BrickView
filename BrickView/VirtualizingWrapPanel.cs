using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace BrickView;

public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
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

    public static readonly RoutedEvent ViewportChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ViewportChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(VirtualizingWrapPanel));

    public VirtualizingWrapPanel()
    {
        thumbnailSizeManager =
            ThumbnailSizeManager.Instance;

        itemWidth =
            thumbnailSizeManager.Current.ItemWidth;

        itemHeight =
            thumbnailSizeManager.Current.ItemHeight;

        thumbnailSizeManager.SizeChanged +=
            ThumbnailSizeManager_SizeChanged;
    }

    public event RoutedEventHandler ViewportChanged
    {
        add
        {
            AddHandler(
                ViewportChangedEvent,
                value);
        }

        remove
        {
            RemoveHandler(
                ViewportChangedEvent,
                value);
        }
    }

    private ItemsControl? ItemsOwner
    {
        get
        {
            return ItemsControl.GetItemsOwner(
                this);
        }
    }

    protected override Size MeasureOverride(
        Size availableSize)
    {
        ItemsControl? itemsOwner =
            ItemsOwner;

        if (itemsOwner is null)
        {
            return availableSize;
        }

        int itemCount =
            itemsOwner.Items.Count;

        if (itemCount == 0)
        {
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
            availableWidth <= 0)
        {
            availableWidth =
                itemWidth;
        }

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

        verticalOffset =
            ClampVerticalOffset(
                verticalOffset);

        GenerateVisibleItems(
            itemCount,
            availableSize);

        scrollOwner?.InvalidateScrollInfo();

        return availableSize;
    }

    protected override Size ArrangeOverride(
        Size finalSize)
    {
        IItemContainerGenerator generator =
            ItemContainerGenerator;

        for (
            int i = 0;
            i < InternalChildren.Count;
            i++)
        {
            UIElement child =
                InternalChildren[i];

            GeneratorPosition position =
                new GeneratorPosition(
                    i,
                    0);

            int itemIndex =
                generator.IndexFromGeneratorPosition(
                    position);

            if (itemIndex < 0)
            {
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

    private void GenerateVisibleItems(
        int itemCount,
        Size availableSize)
    {
        if (itemCount == 0)
        {
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

        int childIndex =
            startPosition.Offset == 0
                ? startPosition.Index
                : startPosition.Index + 1;

        using (
            generator.StartAt(
                startPosition,
                GeneratorDirection.Forward,
                true))
        {
            for (
                int itemIndex =
                    firstVisibleIndex;
                itemIndex <= lastVisibleIndex;
                itemIndex++)
            {
                UIElement? child =
                    generator.GenerateNext(
                        out bool newlyRealized)
                    as UIElement;

                if (child is null)
                {
                    continue;
                }

                if (newlyRealized)
                {
                    if (childIndex >=
                        InternalChildren.Count)
                    {
                        AddInternalChild(
                            child);
                    }
                    else
                    {
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

        CleanupItems(
            firstVisibleIndex,
            lastVisibleIndex);

        if (
            firstVisibleIndex !=
                lastReportedFirstVisibleIndex ||
            lastVisibleIndex !=
                lastReportedLastVisibleIndex)
        {
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

    private void CleanupItems(
        int firstVisibleIndex,
        int lastVisibleIndex)
    {
        IItemContainerGenerator generator =
            ItemContainerGenerator;

        for (
            int i =
                InternalChildren.Count - 1;
            i >= 0;
            i--)
        {
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
                    lastVisibleIndex)
            {
                generator.Remove(
                    position,
                    1);

                RemoveInternalChildRange(
                    i,
                    1);
            }
        }
    }

    private void ThumbnailSizeManager_SizeChanged(
        ThumbnailSizeDefinition newSize)
    {
        itemWidth =
            newSize.ItemWidth;

        itemHeight =
            newSize.ItemHeight;

        lastReportedFirstVisibleIndex =
            -1;

        lastReportedLastVisibleIndex =
            -1;

        InvalidateMeasure();

        scrollOwner?.InvalidateScrollInfo();
    }

    protected override void OnItemsChanged(
        object sender,
        ItemsChangedEventArgs args)
    {
        lastReportedFirstVisibleIndex = -1;

        lastReportedLastVisibleIndex = -1;

        InvalidateMeasure();

        base.OnItemsChanged(
            sender,
            args);
    }

    #region IScrollInfo

    public bool CanHorizontallyScroll
    {
        get;
        set;
    }

    public bool CanVerticallyScroll
    {
        get;
        set;
    }

    public double ExtentHeight
    {
        get
        {
            return extent.Height;
        }
    }

    public double ExtentWidth
    {
        get
        {
            return extent.Width;
        }
    }

    public double HorizontalOffset
    {
        get
        {
            return horizontalOffset;
        }
    }

    public double VerticalOffset
    {
        get
        {
            return verticalOffset;
        }
    }

    public double ViewportHeight
    {
        get
        {
            return viewport.Height;
        }
    }

    public double ViewportWidth
    {
        get
        {
            return viewport.Width;
        }
    }

    public ScrollViewer? ScrollOwner
    {
        get
        {
            return scrollOwner;
        }

        set
        {
            scrollOwner = value;
        }
    }

    public void LineDown()
    {
        SetVerticalOffset(
            verticalOffset +
            itemHeight);
    }

    public void LineUp()
    {
        SetVerticalOffset(
            verticalOffset -
            itemHeight);
    }

    public void LineLeft()
    {
        SetHorizontalOffset(
            horizontalOffset -
            itemWidth);
    }

    public void LineRight()
    {
        SetHorizontalOffset(
            horizontalOffset +
            itemWidth);
    }

    public void MouseWheelDown()
    {
        SetVerticalOffset(
            verticalOffset +
            itemHeight);
    }

    public void MouseWheelUp()
    {
        SetVerticalOffset(
            verticalOffset -
            itemHeight);
    }

    public void MouseWheelLeft()
    {
        SetHorizontalOffset(
            horizontalOffset -
            itemWidth);
    }

    public void MouseWheelRight()
    {
        SetHorizontalOffset(
            horizontalOffset +
            itemWidth);
    }

    public void PageDown()
    {
        SetVerticalOffset(
            verticalOffset +
            viewport.Height);
    }

    public void PageUp()
    {
        SetVerticalOffset(
            verticalOffset -
            viewport.Height);
    }

    public void PageLeft()
    {
        SetHorizontalOffset(
            horizontalOffset -
            viewport.Width);
    }

    public void PageRight()
    {
        SetHorizontalOffset(
            horizontalOffset +
            viewport.Width);
    }

    public void SetHorizontalOffset(
        double offset)
    {
        if (!CanHorizontallyScroll)
        {
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

        if (
            Math.Abs(
                newOffset -
                horizontalOffset)
            < 0.1)
        {
            return;
        }

        horizontalOffset =
            newOffset;

        InvalidateMeasure();

        scrollOwner?.InvalidateScrollInfo();
    }

    public void SetVerticalOffset(
        double offset)
    {
        if (!CanVerticallyScroll)
        {
            return;
        }

        double newOffset =
            ClampVerticalOffset(
                offset);

        if (
            Math.Abs(
                newOffset -
                verticalOffset)
            < 0.1)
        {
            return;
        }

        verticalOffset =
            newOffset;

        InvalidateMeasure();

        scrollOwner?.InvalidateScrollInfo();
    }

    public Rect MakeVisible(
        Visual visual,
        Rect rectangle)
    {
        if (visual is null)
        {
            return rectangle;
        }

        if (rectangle.Top < 0)
        {
            SetVerticalOffset(
                verticalOffset +
                rectangle.Top);
        }
        else if (
            rectangle.Bottom >
            ViewportHeight)
        {
            SetVerticalOffset(
                verticalOffset +
                rectangle.Bottom -
                ViewportHeight);
        }

        return rectangle;
    }

    private double ClampVerticalOffset(
        double offset)
    {
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