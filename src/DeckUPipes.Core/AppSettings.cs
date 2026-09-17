using System.Globalization;

namespace DeckUPipes.Core;

/// <summary>
/// Screen anchor for the overlay. There is one vertical composition; the side only
/// chooses which top corner it hangs from.
/// </summary>
public enum OverlaySide
{
    TopLeft,
    TopRight,
}

public enum MonitorSelection
{
    Cursor,
    Fixed,
}

public enum Theme
{
    Dark,
    Light,
}

/// <summary>Which overlay to show: the current one, or the original classic look.</summary>
public enum UiMode
{
    Modern,
    Legacy,
}

public sealed class AppSettings
{
    public const string DefaultAccentHex = "#FF4EC9B0";

    public string Hotkey { get; set; } = "Control+Alt+M";

    public OverlaySide Side { get; set; } = OverlaySide.TopLeft;

    public UiMode UiMode { get; set; } = UiMode.Modern;

    public int EdgeOffset { get; set; } = 12;

    public double FocusedOpacity { get; set; } = 0.95;

    public double DimOpacity { get; set; } = 0.55;

    public MonitorSelection MonitorMode { get; set; } = MonitorSelection.Cursor;

    public int MonitorIndex { get; set; }

    public bool LaunchAtStartup { get; set; }

    public string AccentColor { get; set; } = DefaultAccentHex;

    public Theme Theme { get; set; } = Theme.Dark;

    public string? SkinId { get; set; }

    public void Clamp()
    {
        EdgeOffset = Math.Clamp(EdgeOffset, 0, 200);
        FocusedOpacity = Math.Clamp(FocusedOpacity, 0.1, 1.0);
        DimOpacity = Math.Clamp(DimOpacity, 0.05, FocusedOpacity);
        MonitorIndex = Math.Max(0, MonitorIndex);
        if (string.IsNullOrWhiteSpace(Hotkey))
        {
            Hotkey = "Control+Alt+M";
        }

        if (!TryNormalizeColor(AccentColor, out var normalized))
        {
            AccentColor = DefaultAccentHex;
        }
        else
        {
            AccentColor = normalized;
        }
    }

    /// Accepts "#RRGGBB" or "#AARRGGBB"; normalizes to "#AARRGGBB".
    private static bool TryNormalizeColor(string? hex, out string normalized)
    {
        normalized = DefaultAccentHex;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var s = hex.Trim().TrimStart('#');
        if (s.Length is not (6 or 8))
        {
            return false;
        }

        if (s.Length == 6)
        {
            s = "FF" + s;
        }

        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ||
            !byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ||
            !byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ||
            !byte.TryParse(s.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        normalized = "#" + s;
        return true;
    }
}

