using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeckUPipes.App.Services;

/// <summary>
/// Loads the embedded logo (Assets/logowo.png) as a frozen WPF image source
/// and as a 32x32 tray Icon. Returns null if the resource is missing so
/// callers can fall back gracefully.
/// </summary>
public static class LogoService
{
    private const string PackUri = "pack://application:,,,/DeckUPipes.App;component/assets/deckupipes.png";

    private static ImageSource? _bitmapSource;
    private static System.Drawing.Icon? _icon;

    public static ImageSource? GetBitmapSource()
    {
        if (_bitmapSource is not null)
        {
            return _bitmapSource;
        }

        try
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.UriSource = new Uri(PackUri, UriKind.Absolute);
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.EndInit();
            source.Freeze();
            _bitmapSource = source;
        }
        catch (Exception)
        {
            return null;
        }

        return _bitmapSource;
    }

    public static System.Drawing.Icon? GetIcon()
    {
        if (_icon is not null)
        {
            return _icon;
        }

        var source = GetBitmapSource() as BitmapSource;
        if (source is null)
        {
            return null;
        }

        try
        {
            using var full = ToBitmap(source);
            using var small = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(small))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(full, 0, 0, 32, 32);
            }

            _icon = System.Drawing.Icon.FromHandle(small.GetHicon());
        }
        catch (Exception)
        {
            return null;
        }

        return _icon;
    }

    private static Bitmap ToBitmap(BitmapSource source)
    {
        var bitmap = new Bitmap(source.PixelWidth, source.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            source.CopyPixels(
                Int32Rect.Empty,
                data.Scan0,
                data.Stride * data.Height,
                data.Stride);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}

