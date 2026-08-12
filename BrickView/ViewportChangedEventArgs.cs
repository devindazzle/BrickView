using System.Windows;

namespace BrickView;

public class ViewportChangedEventArgs : RoutedEventArgs
{
    public int FirstVisibleIndex { get; }

    public int LastVisibleIndex { get; }

    public ViewportChangedEventArgs(RoutedEvent routedEvent, int firstVisibleIndex, int lastVisibleIndex) : base(routedEvent)
    {
        FirstVisibleIndex = firstVisibleIndex;
        LastVisibleIndex = lastVisibleIndex;
    }
}