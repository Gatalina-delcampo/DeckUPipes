using DeckUPipes.Core;

namespace DeckUPipes.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Load_Returns_Defaults_When_File_Missing()
    {
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json"));
        store.Load();

        Assert.Equal("Control+Alt+M", store.Settings.Hotkey);
        Assert.Equal(OverlaySide.TopLeft, store.Settings.Side);
        Assert.True(store.IsFirstRun);
    }

    [Fact]
    public void Load_Marks_FirstRun_Only_When_File_Missing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        var store = new SettingsStore(path);
        store.Load();
        Assert.True(store.IsFirstRun);
        store.Save();

        var reloaded = new SettingsStore(path);
        reloaded.Load();
        Assert.False(reloaded.IsFirstRun);
    }

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        var store = new SettingsStore(path);
        store.Load();
        store.Update(s =>
        {
            s.Hotkey = "Control+Shift+E";
            s.Side = OverlaySide.TopRight;
            s.EdgeOffset = 42;
            s.FocusedOpacity = 0.77;
        });

        var reloaded = new SettingsStore(path);
        reloaded.Load();

        Assert.Equal("Control+Shift+E", reloaded.Settings.Hotkey);
        Assert.Equal(OverlaySide.TopRight, reloaded.Settings.Side);
        Assert.Equal(42, reloaded.Settings.EdgeOffset);
        Assert.Equal(0.77, reloaded.Settings.FocusedOpacity);
    }

    [Fact]
    public void Load_Clamps_Invalid_Values()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"Hotkey":"","EdgeOffset":99999,"FocusedOpacity":5.0}""");

        var store = new SettingsStore(path);
        store.Load();

        Assert.Equal("Control+Alt+M", store.Settings.Hotkey);
        Assert.Equal(200, store.Settings.EdgeOffset);
        Assert.Equal(1.0, store.Settings.FocusedOpacity);
    }

    [Fact]
    public void Load_Clamps_MonitorIndex_And_DimOpacity()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"MonitorIndex":-5,"DimOpacity":0.99,"FocusedOpacity":0.5}""");

        var store = new SettingsStore(path);
        store.Load();

        Assert.Equal(0, store.Settings.MonitorIndex);
        Assert.Equal(0.5, store.Settings.DimOpacity);
    }

    [Theory]
    [InlineData("Right", OverlaySide.TopRight)]
    [InlineData("TopRight", OverlaySide.TopRight)]
    [InlineData("BottomRight", OverlaySide.TopRight)]
    [InlineData("Left", OverlaySide.TopLeft)]
    [InlineData("TopLeft", OverlaySide.TopLeft)]
    [InlineData("TopCenter", OverlaySide.TopLeft)]
    [InlineData("Bottom", OverlaySide.TopLeft)]
    [InlineData("BottomLeft", OverlaySide.TopLeft)]
    [InlineData("BottomCenter", OverlaySide.TopLeft)]
    public void Load_Maps_Every_Retired_Side_Onto_The_Two_Anchors(string stored, OverlaySide expected)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{"Side":"{{stored}}","Hotkey":"Control+Alt+M"}""");

        var store = new SettingsStore(path);
        store.Load();

        Assert.Equal(expected, store.Settings.Side);
    }

    [Fact]
    public void Load_Keeps_The_Rest_Of_The_Config_When_A_Side_Is_Retired()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"Side":"Bottom","Hotkey":"Control+Alt+M","AccentColor":"#FFF5605A"}""");

        var store = new SettingsStore(path);
        store.Load();

        // A retired value must be mapped, not rejected: rejection would silently
        // discard the whole file and reset every other setting.
        Assert.Equal(OverlaySide.TopLeft, store.Settings.Side);
        Assert.Equal("#FFF5605A", store.Settings.AccentColor);
        Assert.Equal("Control+Alt+M", store.Settings.Hotkey);
    }

    [Fact]
    public void Load_Does_Not_Rewrite_A_Hotkey_That_Contains_A_Retired_Side_Name()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"Side":"Left","Hotkey":"Control+Right"}""");

        var store = new SettingsStore(path);
        store.Load();

        Assert.Equal(OverlaySide.TopLeft, store.Settings.Side);
        Assert.Equal("Control+Right", store.Settings.Hotkey);
    }

    [Fact]
    public void Save_Then_Load_RoundTrips_New_Fields()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        var store = new SettingsStore(path);
        store.Load();
        store.Update(s =>
        {
            s.MonitorMode = MonitorSelection.Fixed;
            s.MonitorIndex = 1;
            s.LaunchAtStartup = true;
            s.DimOpacity = 0.3;
            s.AccentColor = "#FF4E8DF7";
            s.Theme = Theme.Light;
            s.SkinId = "neon-noir";
        });

        var reloaded = new SettingsStore(path);
        reloaded.Load();

        Assert.Equal(MonitorSelection.Fixed, reloaded.Settings.MonitorMode);
        Assert.Equal(1, reloaded.Settings.MonitorIndex);
        Assert.True(reloaded.Settings.LaunchAtStartup);
        Assert.Equal(0.3, reloaded.Settings.DimOpacity);
        Assert.Equal("#FF4E8DF7", reloaded.Settings.AccentColor);
        Assert.Equal(Theme.Light, reloaded.Settings.Theme);
        Assert.Equal("neon-noir", reloaded.Settings.SkinId);
    }

    [Fact]
    public void Load_Normalizes_Accent_Color()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"AccentColor":"#4EC9B0","Theme":"Light"}""");

        var store = new SettingsStore(path);
        store.Load();

        Assert.Equal("#FF4EC9B0", store.Settings.AccentColor);
        Assert.Equal(Theme.Light, store.Settings.Theme);
    }

    [Fact]
    public void Load_Falls_Back_To_Default_Accent_When_Invalid()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"AccentColor":"not-a-color"}""");

        var store = new SettingsStore(path);
        store.Load();

        Assert.Equal(AppSettings.DefaultAccentHex, store.Settings.AccentColor);
    }
}

