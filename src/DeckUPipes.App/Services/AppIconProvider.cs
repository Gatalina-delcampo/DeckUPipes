using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GdiColor = System.Drawing.Color;
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DeckUPipes.App.Services;

/// <summary>
/// Extracts application icons from their executable resources. A large icon is
/// requested so the overlay stays sharp on high-DPI monitors; the shell's 32x32
/// icon is only a fallback for programs that ship no large resource.
/// </summary>
public static class AppIconProvider
{
    private const int IconSize = 256;

    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static BitmapSource? GetIcon(string iconPath, string processPath)
    {
        var key = !string.IsNullOrEmpty(iconPath) ? iconPath : processPath;
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var image = Extract(iconPath) ?? Extract(processPath);
            Cache[key] = image;
            return image;
        }
    }

    private static BitmapSource? Extract(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var (file, index) = SplitIndex(path);
        if (file.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            return FromIconFile(file);
        }

        return FromResource(file, index) ?? FromShell(file);
    }

    private static BitmapSource? FromIconFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames
                .OrderByDescending(candidate => candidate.PixelWidth * candidate.PixelHeight)
                .FirstOrDefault();
            if (frame is null)
            {
                return null;
            }

            frame.Freeze();
            return frame;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static (string File, int Index) SplitIndex(string path)
    {
        var file = path.Trim().Trim('"');
        var separator = file.LastIndexOf(',');
        if (separator <= 0)
        {
            return (file, 0);
        }

        return int.TryParse(file[(separator + 1)..], out var index) && index >= 0
            ? (file[..separator], index)
            : (file, 0);
    }

    private static BitmapSource? FromResource(string path, int index)
    {
        var handles = new IntPtr[1];
        var identifiers = new uint[1];
        if (PrivateExtractIcons(path, index, IconSize, IconSize, handles, identifiers, 1, 0) == 0 || handles[0] == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return ToBitmapSource(handles[0]);
        }
        finally
        {
            DestroyIcon(handles[0]);
        }
    }

    private static BitmapSource? FromShell(string path)
    {
        try
        {
            var isLibrary = path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            using var icon = isLibrary ? new Icon(path) : Icon.ExtractAssociatedIcon(path);
            return icon is null ? null : ToBitmapSource(icon.Handle);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static BitmapSource ToBitmapSource(IntPtr handle)
    {
        using var source = Bitmap.FromHicon(handle);
        using var bitmap = new Bitmap(source.Width, source.Height, GdiPixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(GdiColor.Transparent);
            graphics.DrawImageUnscaled(source, 0, 0);
        }

        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            GdiPixelFormat.Format32bppArgb);
        try
        {
            var image = BitmapSource.Create(
                bitmap.Width,
                bitmap.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                data.Scan0,
                data.Stride * bitmap.Height,
                data.Stride);
            image.Freeze();
            return image;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr[] phicon,
        uint[] piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
