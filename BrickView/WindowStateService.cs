// -----------------------------------------------------------------------------
// WindowStateService.cs
//
// Manages persistence and restoration of BrickView's main-window state.
//
// Responsibilities:
// - Restores the saved window size and position.
// - Restores persisted application state through ApplicationStateService.
// - Saves the current window state when requested by MainWindow.
// - Validates the restored window position against the currently available
//   monitor work areas.
// - Moves a restored window back onto a valid monitor when its previous
//   position is no longer available.
//
// The service uses Windows-specific interop only for validating monitor and
// window coordinates. Application state persistence itself is delegated to
// ApplicationStateService.
//
// Restoring the window position is intentionally performed after the WPF
// Window.Loaded event so that a valid native window handle is available.
// -----------------------------------------------------------------------------

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BrickView;

/// <summary>
/// Persists, restores and validates the main BrickView window state.
/// </summary>
/// <remarks>
/// The service coordinates application-state persistence through
/// <see cref="ApplicationStateService"/> and uses Win32 monitor information to
/// ensure that a restored window remains accessible on one of the currently
/// available monitors.
/// </remarks>
public sealed class WindowStateService {
    private readonly ApplicationStateService applicationStateService;

    /// <summary>
    /// Initializes a new window-state service.
    /// </summary>
    /// <param name="applicationStateService">
    /// The service used to load and save the persisted application state.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="applicationStateService"/> is null.
    /// </exception>
    public WindowStateService(
        ApplicationStateService applicationStateService) {
        ArgumentNullException.ThrowIfNull(
            applicationStateService);

        this.applicationStateService =
            applicationStateService;
    }

    /// <summary>
    /// Restores the persisted window state and schedules a position validation
    /// after the window has loaded.
    /// </summary>
    /// <param name="window">
    /// The WPF window whose saved state should be restored.
    /// </param>
    /// <returns>
    /// The persisted <see cref="ApplicationState"/>, or
    /// <see langword="null"/> when no saved state is available.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="window"/> is null.
    /// </exception>
    public ApplicationState? Restore(
        Window window) {
        ArgumentNullException.ThrowIfNull(
            window);

        ApplicationState? state =
            applicationStateService.Load();

        if (state is null) {
            return null;
        }

        if (state.WindowWidth.HasValue &&
            state.WindowWidth.Value > 0) {
            window.Width =
                state.WindowWidth.Value;
        }

        if (state.WindowHeight.HasValue &&
            state.WindowHeight.Value > 0) {
            window.Height =
                state.WindowHeight.Value;
        }

        if (state.WindowLeft.HasValue) {
            window.Left =
                state.WindowLeft.Value;
        }

        if (state.WindowTop.HasValue) {
            window.Top =
                state.WindowTop.Value;
        }

        // The native window handle is not guaranteed to be available until
        // the WPF window has loaded, so position validation is deferred.
        window.Loaded +=
            Window_Loaded;

        return state;
    }

    /// <summary>
    /// Saves the current window geometry and the associated BrickView state.
    /// </summary>
    /// <param name="window">
    /// The WPF window whose current geometry should be persisted.
    /// </param>
    /// <param name="lastSelectedFolder">
    /// The folder currently selected by the user.
    /// </param>
    /// <param name="thumbnailSizePreset">
    /// The currently selected thumbnail-size preset.
    /// </param>
    /// <param name="sortField">
    /// The field currently used for sorting.
    /// </param>
    /// <param name="sortDirection">
    /// The currently selected sort direction.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="window"/> is null.
    /// </exception>
    public void Save(
        Window window,
        string? lastSelectedFolder,
        ThumbnailSizePreset thumbnailSizePreset,
        FileSortField sortField = FileSortField.FileName,
        FileSortDirection sortDirection = FileSortDirection.Ascending) {
        ArgumentNullException.ThrowIfNull(
            window);

        ApplicationState state =
            new ApplicationState {
                WindowLeft = window.Left,
                WindowTop = window.Top,
                WindowWidth = window.Width,
                WindowHeight = window.Height,
                LastSelectedFolder = lastSelectedFolder,
                ThumbnailSizePreset = thumbnailSizePreset,
                SortField = sortField,
                SortDirection = sortDirection
            };

        applicationStateService.Save(
            state);
    }

    /// <summary>
    /// Handles the window's Loaded event and validates the restored native
    /// window position against the available monitor work areas.
    /// </summary>
    /// <param name="sender">
    /// The window that raised the Loaded event.
    /// </param>
    /// <param name="e">
    /// Routed event data supplied by WPF.
    /// </param>
    private void Window_Loaded(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Window window) {
            return;
        }

        // Validate only once. The subscription is no longer needed after the
        // initial restored position has been checked.
        window.Loaded -=
            Window_Loaded;

        ValidateWindowPosition(
            window);
    }

    /// <summary>
    /// Ensures that a restored window position is fully contained within one
    /// of the currently available monitor work areas.
    /// </summary>
    /// <param name="window">
    /// The window whose native position should be validated.
    /// </param>
    private static void ValidateWindowPosition(
        Window window) {
        WindowInteropHelper windowInteropHelper =
            new WindowInteropHelper(
                window);

        IntPtr windowHandle =
            windowInteropHelper.Handle;

        if (windowHandle == IntPtr.Zero) {
            return;
        }

        if (!GetWindowRect(
                windowHandle,
                out RECT windowRect)) {
            return;
        }

        List<RECT> monitorWorkAreas =
            GetMonitorWorkAreas();

        if (monitorWorkAreas.Count == 0) {
            return;
        }

        RECT? containingWorkArea =
            FindContainingWorkArea(
                windowRect,
                monitorWorkAreas);

        if (containingWorkArea.HasValue) {
            return;
        }

        // When the previous monitor is no longer available, choose the work
        // area with the greatest overlap and reposition the window there.
        RECT targetWorkArea =
            FindBestWorkArea(
                windowRect,
                monitorWorkAreas);

        int windowWidth =
            windowRect.Right -
            windowRect.Left;

        int windowHeight =
            windowRect.Bottom -
            windowRect.Top;

        int correctedLeft =
            CalculateCorrectedCoordinate(
                windowRect.Left,
                windowWidth,
                targetWorkArea.Left,
                targetWorkArea.Right);

        int correctedTop =
            CalculateCorrectedCoordinate(
                windowRect.Top,
                windowHeight,
                targetWorkArea.Top,
                targetWorkArea.Bottom);

        // Only the position is corrected. The persisted window size and z-order
        // are intentionally left untouched.
        SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            correctedLeft,
            correctedTop,
            0,
            0,
            SWP_NOSIZE |
            SWP_NOZORDER |
            SWP_NOACTIVATE);
    }

    /// <summary>
    /// Finds a monitor work area that completely contains the restored window.
    /// </summary>
    /// <param name="windowRect">
    /// The current native window rectangle.
    /// </param>
    /// <param name="monitorWorkAreas">
    /// The available monitor work areas.
    /// </param>
    /// <returns>
    /// The containing work area, or <see langword="null"/> when no work area
    /// contains the complete window.
    /// </returns>
    private static RECT? FindContainingWorkArea(
        RECT windowRect,
        List<RECT> monitorWorkAreas) {
        foreach (RECT workArea
                 in monitorWorkAreas) {
            if (windowRect.Left >= workArea.Left &&
                windowRect.Top >= workArea.Top &&
                windowRect.Right <= workArea.Right &&
                windowRect.Bottom <= workArea.Bottom) {
                return workArea;
            }
        }

        return null;
    }

    /// <summary>
    /// Selects the monitor work area with the greatest overlap with the
    /// current window rectangle.
    /// </summary>
    /// <param name="windowRect">
    /// The current native window rectangle.
    /// </param>
    /// <param name="monitorWorkAreas">
    /// The available monitor work areas.
    /// </param>
    /// <returns>
    /// The work area with the greatest intersection with the window.
    /// </returns>
    private static RECT FindBestWorkArea(
        RECT windowRect,
        List<RECT> monitorWorkAreas) {
        long bestOverlapArea =
            -1;

        RECT bestWorkArea =
            monitorWorkAreas[0];

        foreach (RECT workArea
                 in monitorWorkAreas) {
            long overlapArea =
                CalculateIntersectionArea(
                    windowRect,
                    workArea);

            if (overlapArea > bestOverlapArea) {
                bestOverlapArea =
                    overlapArea;

                bestWorkArea =
                    workArea;
            }
        }

        return bestWorkArea;
    }

    /// <summary>
    /// Calculates the area where two rectangles overlap.
    /// </summary>
    /// <param name="first">
    /// The first rectangle.
    /// </param>
    /// <param name="second">
    /// The second rectangle.
    /// </param>
    /// <returns>
    /// The overlapping area in square pixels, or zero when the rectangles
    /// do not intersect.
    /// </returns>
    private static long CalculateIntersectionArea(
        RECT first,
        RECT second) {
        int left =
            Math.Max(
                first.Left,
                second.Left);

        int top =
            Math.Max(
                first.Top,
                second.Top);

        int right =
            Math.Min(
                first.Right,
                second.Right);

        int bottom =
            Math.Min(
                first.Bottom,
                second.Bottom);

        if (right <= left ||
            bottom <= top) {
            return 0;
        }

        return (long)(right - left) *
               (bottom - top);
    }

    /// <summary>
    /// Calculates a window coordinate constrained to a monitor work area.
    /// </summary>
    /// <param name="currentPosition">
    /// The current position of the window edge.
    /// </param>
    /// <param name="windowSize">
    /// The size of the window along the relevant axis.
    /// </param>
    /// <param name="workAreaStart">
    /// The start coordinate of the monitor work area.
    /// </param>
    /// <param name="workAreaEnd">
    /// The end coordinate of the monitor work area.
    /// </param>
    /// <returns>
    /// A position that keeps the window inside the available work area when
    /// the window is smaller than the work area.
    /// </returns>
    private static int CalculateCorrectedCoordinate(
        int currentPosition,
        int windowSize,
        int workAreaStart,
        int workAreaEnd) {
        int workAreaSize =
            workAreaEnd -
            workAreaStart;

        if (windowSize >= workAreaSize) {
            // A window larger than the work area cannot fit entirely inside it.
            // Align its leading edge with the work-area start.
            return workAreaStart;
        }

        int minimumPosition =
            workAreaStart;

        int maximumPosition =
            workAreaEnd -
            windowSize;

        return Math.Min(
            Math.Max(
                currentPosition,
                minimumPosition),
            maximumPosition);
    }

    /// <summary>
    /// Retrieves the usable work areas for all currently connected monitors.
    /// </summary>
    /// <returns>
    /// A list containing the work area of each monitor.
    /// </returns>
    private static List<RECT> GetMonitorWorkAreas() {
        List<RECT> workAreas =
            new List<RECT>();

        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (
                IntPtr monitorHandle,
                IntPtr deviceContext,
                ref RECT monitorRectangle,
                IntPtr data) => {
                    MONITORINFO monitorInfo =
                        new MONITORINFO();

                    monitorInfo.cbSize =
                        Marshal.SizeOf<MONITORINFO>();

                    if (GetMonitorInfo(
                            monitorHandle,
                            ref monitorInfo)) {
                        // rcWork excludes taskbars and other reserved desktop
                        // regions, which is the appropriate area for window placement.
                        workAreas.Add(
                            monitorInfo.rcWork);
                    }

                    return true;
                },
            IntPtr.Zero);

        return workAreas;
    }

    /// <summary>
    /// Retrieves the screen-space rectangle occupied by a native window.
    /// </summary>
    /// <param name="hWnd">
    /// The native window handle.
    /// </param>
    /// <param name="lpRect">
    /// Receives the native window rectangle.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the rectangle was retrieved successfully.
    /// </returns>
    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool GetWindowRect(
        IntPtr hWnd,
        out RECT lpRect);

    /// <summary>
    /// Retrieves monitor information, including the usable work area.
    /// </summary>
    /// <param name="hMonitor">
    /// The native monitor handle.
    /// </param>
    /// <param name="lpmi">
    /// Receives the monitor information.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the information was retrieved successfully.
    /// </returns>
    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool GetMonitorInfo(
        IntPtr hMonitor,
        ref MONITORINFO lpmi);

    /// <summary>
    /// Enumerates the currently available display monitors.
    /// </summary>
    /// <param name="hdc">
    /// Reserved device-context parameter.
    /// </param>
    /// <param name="lprcClip">
    /// Optional clipping rectangle.
    /// </param>
    /// <param name="lpfnEnum">
    /// Callback invoked for each enumerated monitor.
    /// </param>
    /// <param name="dwData">
    /// Application-defined callback data.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when monitor enumeration succeeds.
    /// </returns>
    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    /// <summary>
    /// Changes a native window's position and selected window-management flags.
    /// </summary>
    /// <param name="hWnd">
    /// The native window handle.
    /// </param>
    /// <param name="hWndInsertAfter">
    /// The handle used for z-order positioning.
    /// </param>
    /// <param name="x">
    /// The new horizontal screen coordinate.
    /// </param>
    /// <param name="y">
    /// The new vertical screen coordinate.
    /// </param>
    /// <param name="width">
    /// The requested window width.
    /// </param>
    /// <param name="height">
    /// The requested window height.
    /// </param>
    /// <param name="flags">
    /// Flags controlling which aspects of the window position operation change.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the operation succeeds.
    /// </returns>
    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    /// <summary>
    /// Defines the callback signature used by EnumDisplayMonitors.
    /// </summary>
    /// <param name="hMonitor">
    /// The handle of the enumerated monitor.
    /// </param>
    /// <param name="hdcMonitor">
    /// The device context associated with the monitor.
    /// </param>
    /// <param name="lprcMonitor">
    /// The monitor rectangle.
    /// </param>
    /// <param name="dwData">
    /// Application-defined callback data.
    /// </param>
    /// <returns>
    /// <see langword="true"/> to continue enumeration; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData);

    /// <summary>
    /// Represents the native RECT structure used by the Windows API.
    /// </summary>
    [StructLayout(
        LayoutKind.Sequential)]
    private struct RECT {
        /// <summary>
        /// Gets or sets the left screen coordinate.
        /// </summary>
        public int Left;

        /// <summary>
        /// Gets or sets the top screen coordinate.
        /// </summary>
        public int Top;

        /// <summary>
        /// Gets or sets the right screen coordinate.
        /// </summary>
        public int Right;

        /// <summary>
        /// Gets or sets the bottom screen coordinate.
        /// </summary>
        public int Bottom;
    }

    /// <summary>
    /// Represents the native MONITORINFO structure used by the Windows API.
    /// </summary>
    [StructLayout(
        LayoutKind.Sequential)]
    private struct MONITORINFO {
        /// <summary>
        /// Gets or sets the size of this structure in bytes.
        /// </summary>
        public int cbSize;

        /// <summary>
        /// Gets or sets the complete monitor bounds.
        /// </summary>
        public RECT rcMonitor;

        /// <summary>
        /// Gets or sets the usable monitor work area.
        /// </summary>
        public RECT rcWork;

        /// <summary>
        /// Gets or sets the native monitor-state flags.
        /// </summary>
        public uint dwFlags;
    }

    /// <summary>
    /// Prevents SetWindowPos from changing the window size.
    /// </summary>
    private const uint SWP_NOSIZE =
        0x0001;

    /// <summary>
    /// Prevents SetWindowPos from changing the window's z-order.
    /// </summary>
    private const uint SWP_NOZORDER =
        0x0004;

    /// <summary>
    /// Prevents SetWindowPos from activating the window.
    /// </summary>
    private const uint SWP_NOACTIVATE =
        0x0010;
}