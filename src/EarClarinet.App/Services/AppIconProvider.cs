using System.Drawing;

namespace EarClarinet.App.Services;

public static class AppIconProvider
{
    private static readonly Dictionary<string, Icon?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Icon? GetIcon(string iconPath, string processPath)
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

            var icon = TryExtract(iconPath) ?? TryExtract(processPath);
            Cache[key] = icon;
            return icon;
        }
    }

    private static Icon? TryExtract(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return new Icon(path);
            }

            return Icon.ExtractAssociatedIcon(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
