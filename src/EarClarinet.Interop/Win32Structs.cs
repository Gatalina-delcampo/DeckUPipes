using System.Runtime.InteropServices;

namespace EarClarinet.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;

    public static MONITORINFO Create() => new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
}
