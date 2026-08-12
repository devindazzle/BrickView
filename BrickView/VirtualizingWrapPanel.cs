using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace BrickView;

public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    private const double ItemWidth = 200;
    private const double ItemHeight = 290;

    private Size extent = new Size(0, 0);
    private Size viewport = new Size(0, 0);

    private double horizontalOffset;
    private double verticalOffset;

    private int columnCount = 1;

    private ScrollViewer? scrollOwner;

    private ItemsControl? ItemsOwner
    {
        get
        {
            return ItemsControl.GetItemsOwner(this);
        }
    }

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        ItemsControl? itemsOwner = ItemsOwner;

        if (itemsOwner is null)
        {
            return availableSize;
        }

        int itemCount = itemsOwner.Items.Count;

        if (itemCount == 0)
        {
            extent = new Size(
                availableSize.Width,
                0);

            viewport = availableSize;

            scrollOwner?.InvalidateScrollInfo();

            return availableSize;
        }

        double availableWidth = availableSize.Width;

        if (double.IsInfinity(availableWidth) ||
            availableWidth <= 0)
        {
            availableWidth = ItemWidth;
        }

        columnCount = Math.Max(
            1,
            (int)Math.Floor(
                availableWidth / ItemWidth));

        int rowCount = (int)Math.Ceiling(
            (double)itemCount / columnCount);

        double extentHeight = rowCount * ItemHeight;

        viewport = availableSize;

        extent = new Size(
            availableWidth,
            extentHeight);

        verticalOffset = ClampVerticalOffset(
            verticalOffset);

        GenerateVisibleItems(
            itemCount,
            availableSize);

        scrollOwner?.InvalidateScrollInfo();

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        IItemContainerGenerator generator =
            ItemContainerGenerator;

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            UIElement child = InternalChildren[i];

            GeneratorPosition position =
                new GeneratorPosition(i, 0);

            int itemIndex =
                generator.IndexFromGeneratorPosition(
                    position);

            if (itemIndex < 0)
            {
                continue;
            }

            int row = itemIndex / columnCount;
            int column = itemIndex % columnCount;

            double x = column * ItemWidth;

            double y =
                row * ItemHeight
                - verticalOffset;

            child.Arrange(
                new Rect(
                    x,
                    y,
                    ItemWidth,
                    ItemHeight));
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
            verticalOffset + availableSize.Height;

        int firstVisibleRow = Math.Max(
            0,
            (int)Math.Floor(
                verticalOffset / ItemHeight));

        int lastVisibleRow = Math.Min(
            (int)Math.Ceiling(
                viewportBottom / ItemHeight),
            (int)Math.Ceiling(
                (double)itemCount / columnCount) - 1);

        int firstVisibleIndex =
            firstVisibleRow * columnCount;

        int lastVisibleIndex = Math.Min(
            itemCount - 1,
            ((lastVisibleRow + 1) * columnCount) - 1);

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
                int itemIndex = firstVisibleIndex;
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
                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
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
                        ItemWidth,
                        ItemHeight));

                childIndex++;
            }
        }

        CleanupItems(
            firstVisibleIndex,
            lastVisibleIndex);
    }

    private void CleanupItems(
        int firstVisibleIndex,
        int lastVisibleIndex)
    {
        IItemContainerGenerator generator =
            ItemContainerGenerator;

        for (
            int i = InternalChildren.Count - 1;
            i >= 0;
            i--)
        {
            GeneratorPosition position =
                new GeneratorPosition(i, 0);

            int itemIndex =
                generator.IndexFromGeneratorPosition(
                    position);

            if (
                itemIndex < firstVisibleIndex ||
                itemIndex > lastVisibleIndex)
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

    #endregion

    #region Items changed

    protected override void OnItemsChanged(
        object sender,
        ItemsChangedEventArgs args)
    {
        switch (args.Action)
        {
            case System.Collections.Specialized
                .NotifyCollectionChangedAction.Add:

            case System.Collections.Specialized
                .NotifyCollectionChangedAction.Remove:

            case System.Collections.Specialized
                .NotifyCollectionChangedAction.Replace:

            case System.Collections.Specialized
                .NotifyCollectionChangedAction.Move:

            case System.Collections.Specialized
                .NotifyCollectionChangedAction.Reset:

                InvalidateMeasure();

                break;
        }

        base.OnItemsChanged(
            sender,
            args);
    }

    #endregion

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
            verticalOffset + ItemHeight);
    }

    public void LineUp()
    {
        SetVerticalOffset(
            verticalOffset - ItemHeight);
    }

    public void LineLeft()
    {
        SetHorizontalOffset(
            horizontalOffset - ItemWidth);
    }

    public void LineRight()
    {
        SetHorizontalOffset(
            horizontalOffset + ItemWidth);
    }

    public void MouseWheelDown()
    {
        SetVerticalOffset(
            verticalOffset + ItemHeight);
    }

    public void MouseWheelUp()
    {
        SetVerticalOffset(
            verticalOffset - ItemHeight);
    }

    public void MouseWheelLeft()
    {
        SetHorizontalOffset(
            horizontalOffset - ItemWidth);
    }

    public void MouseWheelRight()
    {
        SetHorizontalOffset(
            horizontalOffset + ItemWidth);
    }

    public void PageDown()
    {
        SetVerticalOffset(
            verticalOffset + viewport.Height);
    }

    public void PageUp()
    {
        SetVerticalOffset(
            verticalOffset - viewport.Height);
    }

    public void PageLeft()
    {
        SetHorizontalOffset(
            horizontalOffset - viewport.Width);
    }

    public void PageRight()
    {
        SetHorizontalOffset(
            horizontalOffset + viewport.Width);
    }

    public void SetHorizontalOffset(
        double offset)
    {
        if (!CanHorizontallyScroll)
        {
            return;
        }

        double newOffset = Math.Max(
            0,
            Math.Min(
                offset,
                Math.Max(
                    0,
                    ExtentWidth - ViewportWidth)));

        if (Math.Abs(
                newOffset - horizontalOffset)
            < 0.1)
        {
            return;
        }

        horizontalOffset = newOffset;

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
            ClampVerticalOffset(offset);

        if (Math.Abs(
                newOffset - verticalOffset)
            < 0.1)
        {
            return;
        }

        verticalOffset = newOffset;

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
                verticalOffset + rectangle.Top);
        }
        else if (
            rectangle.Bottom > ViewportHeight)
        {
            SetVerticalOffset(
                verticalOffset
                + rectangle.Bottom
                - ViewportHeight);
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
                    ExtentHeight - ViewportHeight)));
    }

    #endregion
}