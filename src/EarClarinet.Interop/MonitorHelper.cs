using System.Runtime.InteropServices;

namespace EarClarinet.Interop;

public sealed record MonitorInfo(RECT Bounds, RECT WorkArea, bool IsPrimary);

public static class MonitorHelper
{
    private delegate bool EnumMonitorsProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsProc lpfnEnum, IntPtr dwData);

    private static bool IsPrimary(IntPtr monitor) => monitor == NativeMethods.MonitorFromPointNearest(default);

    public static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<(IntPtr Handle, RECT Monitor, RECT Work)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (handle, _, _, _) =>
        {
            var info = MONITORINFO.Create();
            NativeMethods.GetMonitorInfo(handle, ref info);
            monitors.Add((handle, info.rcMonitor, info.rcWork));
            return true;
        }, IntPtr.Zero);

        return monitors
            .OrderBy(m => m.Work.Left)
            .ThenBy(m => m.Work.Top)
            .Select((m, index) => new MonitorInfo(m.Monitor, m.Work, IsPrimary(m.Handle)))
            .ToList();
    }

    public static RECT GetWorkAreaAt(int index)
    {
        var monitors = GetMonitors();
        if (monitors.Count == 0)
        {
            return default;
        }

        var monitor = index >= 0 && index < monitors.Count ? monitors[index] : monitors[0];
        return monitor.WorkArea;
    }
}
