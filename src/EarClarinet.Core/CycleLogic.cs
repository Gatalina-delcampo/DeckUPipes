namespace EarClarinet.Core;

/// <summary>
/// Pure cycling logic for the overlay's focus model.
/// The focusable items are the SYSTEM row plus every app box; the SYSTEM row
/// is represented by the special app index -1, apps by 0..appCount-1.
/// </summary>
public static class CycleLogic
{
    /// App index of the SYSTEM row.
    public const int SystemAppIndex = -1;

    /// <summary>Returns the app index reached after stepping; -1 means SYSTEM.</summary>
    public static int NextAppIndex(int focusedAppIndex, int appCount, int direction)
    {
        var total = appCount + 1;
        var unified = focusedAppIndex + 1;
        var next = ((unified + direction) % total + total) % total;
        return next - 1;
    }

    /// <summary>
    /// App index of the next TAB target, or null when the next target is SYSTEM
    /// (there is no chip to show then — the SYSTEM row has its own chip).
    /// </summary>
    public static int? NextTabTargetAppIndex(int focusedAppIndex, int appCount)
    {
        var next = NextAppIndex(focusedAppIndex, appCount, 1);
        return next < 0 ? null : next;
    }
}
