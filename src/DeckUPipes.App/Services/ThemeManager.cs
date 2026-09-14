using System.Windows;
using System.Windows.Media;
using DeckUPipes.Core;
using MediaColor = System.Windows.Media.Color;

namespace DeckUPipes.App.Services;

/// <summary>
/// Applies the user's accent color and theme to the application resource
/// brushes. All themed brushes are consumed via DynamicResource so changes
/// propagate instantly to every open window.
/// </summary>
public static class ThemeManager
{
    public static void Apply(AppSettings settings, ResourceDictionary resources)
    {
        var accent = ParseAccent(settings.AccentColor);
        resources["AccentBrush"] = Make(accent);

        var light = settings.Theme == Theme.Light;
        resources["PanelBrush"] = Make(light ? 0xFFF2F2F5 : 0xFF1C1C22);
        resources["PanelBorderBrush"] = Make(light ? 0x33000000 : 0x44FFFFFF);
        resources["AppBoxBrush"] = Make(light ? 0xFFFAFAFB : 0xF523232B);
        resources["AppBoxBorderBrush"] = Make(light ? 0x26000000 : 0x1AFFFFFF);
        resources["TextBrush"] = Make(light ? 0xFF1C1C22 : 0xFFE8E8EC);
        resources["DimTextBrush"] = Make(light ? 0xFF6E6E78 : 0xAAE8E8EC);
        resources["TrackBrush"] = Make(light ? 0xFFE4E4EA : 0x2EFFFFFF);
        resources["HeaderHintBrush"] = Make(light ? 0xFF9A9AA4 : 0xFF3E3E4A);
        resources["InputBrush"] = Make(light ? 0xFFFFFFFF : 0xFF14141A);
        resources["InputBorderBrush"] = Make(light ? 0x33000000 : 0x33FFFFFF);
        resources["EscChipBrush"] = Make(light ? 0x33000000 : 0x3DFFFFFF);
        resources["EscChipBorderBrush"] = Make(light ? 0x66000000 : 0x66FFFFFF);
        resources["MuteBrush"] = Make(light ? 0xFFD64550 : 0xFFFF5F6B);
    }

    private static MediaColor ParseAccent(string hex)
    {
        try
        {
            return (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }
        catch (Exception)
        {
            return (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(AppSettings.DefaultAccentHex);
        }
    }

    private static SolidColorBrush Make(long argb)
    {
        var color = MediaColor.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);
        return Make(color);
    }

    private static SolidColorBrush Make(MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

