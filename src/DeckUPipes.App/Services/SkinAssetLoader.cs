using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeckUPipes.Core;

namespace DeckUPipes.App.Services;

public sealed class SkinAssetLoader
{
    private readonly Dictionary<string, BitmapSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SkinPackage? Package { get; private set; }

    public void Load(SkinPackage? package)
    {
        _cache.Clear();
        Package = package?.Validation.IsValid == true ? package : null;
    }

    public BitmapSource? Get(string slotId)
    {
        if (Package is null || !Package.Manifest.Slots.ContainsKey(slotId))
        {
            return null;
        }

        if (_cache.TryGetValue(slotId, out var cached))
        {
            return cached;
        }

        try
        {
            var path = Package.GetAssetPath(slotId);
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            _cache[slotId] = bitmap;
            return bitmap;
        }
        catch (IOException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    public ImageBrush? GetTileBrush(string slotId)
    {
        var bitmap = Get(slotId);
        if (bitmap is null)
        {
            return null;
        }

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new System.Windows.Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
        };
        brush.Freeze();
        return brush;
    }
}

