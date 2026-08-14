namespace EarClarinet.Core;

public static class SessionGrouping
{
    /// <summary>
    /// Groups sessions for display. The group key decides what merges into one
    /// mixer entry: process name for real processes (so multiple processes of
    /// the same app collapse into one row — e.g. two Discord instances), and
    /// the session id for pid-0 entries (system sounds) so they never merge
    /// with anything.
    /// </summary>
    public static string GroupKey(uint processId, string processName, string instanceId)
    {
        if (processId > 0)
        {
            return $"name:{processName.Trim().ToLowerInvariant()}";
        }

        return $"session:{instanceId}";
    }

    /// <summary>
    /// Produces the ordered display list: one entry per group, preferring the
    /// active member as the representative. Groups are ordered by recency
    /// (the newest LastActiveTicks among their members, so merged processes
    /// with different ages sort by their most recent activity); ties keep the
    /// first-seen group order and never-active entries stay at the bottom.
    /// </summary>
    public static IReadOnlyList<T> SelectDisplaySet<T>(
        IEnumerable<T> sessions,
        Func<T, string> groupKeyOf,
        Func<T, bool> isActiveOf,
        Func<T, long> lastActiveOf)
    {
        var result = new List<(T Display, long Recency)>();
        foreach (var group in sessions.GroupBy(groupKeyOf))
        {
            var display = group.FirstOrDefault(isActiveOf) ?? group.First();
            result.Add((display, group.Max(lastActiveOf)));
        }

        return result.OrderByDescending(r => r.Recency).Select(r => r.Display).ToList();
    }
}
