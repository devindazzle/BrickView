using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BrickView;

public sealed class WindowStateService {
    private readonly ApplicationStateService applicationStateService;

    public WindowStateService(
        ApplicationStateService applicationStateService) {
        this.applicationStateService =
            applicationStateService;
    }

    public ApplicationState? Restore(
        Window window) {
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

        window.Loaded +=
            Window_Loaded;

        return state;
    }

    public void Save(
        Window window,
        string? lastSelectedFolder,
        ThumbnailSizePreset thumbnailSizePreset) {
        ApplicationState state =
            new ApplicationState {
                WindowLeft = window.Left,
                WindowTop = window.Top,
                WindowWidth = window.Width,
                WindowHeight = window.Height,
                LastSelectedFolder = lastSelectedFolder,
                ThumbnailSizePreset = thumbnailSizePreset
            };

        applicationStateService.Save(
            state);
    }

    private void Window_Loaded(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Window window) {
            return;
        }

        window.Loaded -=
            Window_Loaded;

        ValidateWindowPosition(
            window);
    }

    private static void ValidateWindowPosition(
        Window window) {
        WindowInteropHelper windowInteropHelper =
            new WindowInteropHelper(window);

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

    private static int CalculateCorrectedCoordinate(
        int currentPosition,
        int windowSize,
        int workAreaStart,
        int workAreaEnd) {
        int workAreaSize =
            workAreaEnd -
            workAreaStart;

        if (windowSize >= workAreaSize) {
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
                        workAreas.Add(
                            monitorInfo.rcWork);
                    }

                    return true;
                },
            IntPtr.Zero);

        return workAreas;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(
        IntPtr hWnd,
        out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(
        IntPtr hMonitor,
        ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO {
        public int cbSize;

        public RECT rcMonitor;

        public RECT rcWork;

        public uint dwFlags;
    }

    private const uint SWP_NOSIZE = 0x0001;

    private const uint SWP_NOZORDER = 0x0004;

    private const uint SWP_NOACTIVATE = 0x0010;
}