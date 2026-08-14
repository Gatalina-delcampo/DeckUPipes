namespace EarClarinet.Interop;

public static class WorkAreaHelper
{
    public static RECT GetMonitorWorkAreaAtCursor()
    {
        NativeMethods.GetCursorPos(out var point);
        var monitor = NativeMethods.MonitorFromPointNearest(point);
        var info = MONITORINFO.Create();
        NativeMethods.GetMonitorInfo(monitor, ref info);
        return info.rcWork;
    }

    public static RECT GetMonitorWorkAreaAt(int x, int y) =>
        GetMonitorWorkAreaAtPoint(new POINT(x, y));

    private static RECT GetMonitorWorkAreaAtPoint(POINT point)
    {
        var monitor = NativeMethods.MonitorFromPointNearest(point);
        var info = MONITORINFO.Create();
        NativeMethods.GetMonitorInfo(monitor, ref info);
        return info.rcWork;
    }
}
