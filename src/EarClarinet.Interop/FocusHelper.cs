namespace EarClarinet.Interop;

/// <summary>
/// Brings a window to the foreground and gives it keyboard focus. Plain WPF
/// Activate() does not work reliably for layered (AllowsTransparency) windows,
/// so this uses SetForegroundWindow with the classic AttachThreadInput
/// fallback used when the OS foreground lock would otherwise refuse.
/// </summary>
public static class FocusHelper
{
    public static void Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (NativeMethods.SetForegroundWindow(hwnd))
        {
            return;
        }

        // The calling process may not be allowed to steal the foreground; attach
        // our input queue to the current foreground thread and retry.
        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();

        NativeMethods.AttachThreadInput(foregroundThread, currentThread, true);
        try
        {
            NativeMethods.SetForegroundWindow(hwnd);
        }
        finally
        {
            NativeMethods.AttachThreadInput(foregroundThread, currentThread, false);
        }
    }
}
