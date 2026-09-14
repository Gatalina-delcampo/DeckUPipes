using DeckUPipes.Core;

namespace DeckUPipes.Tests;

public class SessionGroupingTests
{
    private sealed record Fake(string Id, string Name, uint Pid, bool Active, long LastActive);

    private static IReadOnlyList<Fake> Group(params Fake[] items) =>
        SessionGrouping.SelectDisplaySet(
            items,
            s => SessionGrouping.GroupKey(s.Pid, s.Name, s.Id),
            s => s.Active,
            s => s.LastActive);

    private static Fake App(string id, string name, bool active = false, long lastActive = 0) =>
        new(id, name, 100u, active, lastActive);

    [Fact]
    public void Single_Session_Passes_Through()
    {
        var result = Group(App("a", "discord", lastActive: 10));

        Assert.Single(result);
        Assert.Equal("a", result[0].Id);
    }

    [Fact]
    public void Sessions_With_Same_Name_Collapse_To_One()
    {
        var result = Group(
            App("d1", "discord", lastActive: 10),
            App("d2", "discord", lastActive: 20),
            App("s1", "steam", lastActive: 5));

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Name == "discord");
    }

    [Fact]
    public void Name_Grouping_Is_Case_Insensitive()
    {
        var result = Group(
            App("d1", "Discord", lastActive: 10),
            App("d2", "discord", lastActive: 20));

        Assert.Single(result);
    }

    [Fact]
    public void Active_Session_Is_Preferred_For_Display()
    {
        var result = Group(
            App("d1", "discord", lastActive: 10),
            App("d2", "discord", active: true, lastActive: 5));

        Assert.Equal("d2", result[0].Id);
    }

    [Fact]
    public void Ordering_Follows_Group_Recency_Not_Member_Display()
    {
        // The merged Discord group has a member active 30s ago (recency max),
        // while the Steam session is only 10s old: Discord sorts first, but
        // the DISPLAYED Discord member is still the active one (d2).
        var result = Group(
            App("s1", "steam", active: true, lastActive: 10),
            App("d1", "discord", lastActive: 30),
            App("d2", "discord", active: true, lastActive: 5));

        Assert.Equal("discord", result[0].Name);
        Assert.Equal("d2", result[0].Id);
        Assert.Equal("steam", result[1].Name);
    }

    [Fact]
    public void Never_Active_Entries_Stay_At_The_Bottom_Stable()
    {
        var result = Group(
            App("a1", "alpha", lastActive: 0),
            App("b1", "beta", lastActive: 0),
            App("c1", "gamma", lastActive: 50));

        Assert.Equal("c1", result[0].Id);
        Assert.Equal("a1", result[1].Id);
        Assert.Equal("b1", result[2].Id);
    }

    [Fact]
    public void Pid_Zero_Sessions_Are_Not_Grouped_By_Name()
    {
        var result = Group(
            new Fake("sys1", "AudioSrv", 0u, false, 0),
            new Fake("sys2", "AudioSrv", 0u, false, 0),
            App("a1", "app", lastActive: 1));

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Empty_Input_Returns_Empty()
    {
        Assert.Empty(Group());
    }
}

