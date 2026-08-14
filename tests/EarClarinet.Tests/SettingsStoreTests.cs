using EarClarinet.Core;

namespace EarClarinet.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Load_Returns_Defaults_When_File_Missing()
    {
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json"));
        store.Load();

        Assert.Equal("Control+Alt+M", store.Settings.Hotkey);
        Assert.Equal(OverlaySide.Left, store.Settings.Side);
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
            s.Side = OverlaySide.Right;
            s.EdgeOffset = 42;
            s.FocusedOpacity = 0.77;
        });

        var reloaded = new SettingsStore(path);
        reloaded.Load();

        Assert.Equal("Control+Shift+E", reloaded.Settings.Hotkey);
        Assert.Equal(OverlaySide.Right, reloaded.Settings.Side);
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

    [Fact]
    public void Save_Then_Load_RoundTrips_Side_Bottom()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        var store = new SettingsStore(path);
        store.Load();
        store.Update(s => s.Side = OverlaySide.BottomCenter);

        var reloaded = new SettingsStore(path);
        reloaded.Load();

        Assert.Equal(OverlaySide.BottomCenter, reloaded.Settings.Side);
    }

    [Fact]
    public void Load_Migrates_Legacy_Bottom_Side()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"Side":"Bottom","Hotkey":"Control+Alt+M","AccentColor":"#FFF5605A"}""");

        var store = new SettingsStore(path);
        store.Load();

        // The old "Bottom" value must not nuke the rest of the config.
        Assert.Equal(OverlaySide.BottomCenter, store.Settings.Side);
        Assert.Equal("#FFF5605A", store.Settings.AccentColor);
        Assert.Equal("Control+Alt+M", store.Settings.Hotkey);
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
            s.ShowShortcutHints = false;
            s.AccentColor = "#FF4E8DF7";
            s.Theme = Theme.Light;
        });

        var reloaded = new SettingsStore(path);
        reloaded.Load();

        Assert.Equal(MonitorSelection.Fixed, reloaded.Settings.MonitorMode);
        Assert.Equal(1, reloaded.Settings.MonitorIndex);
        Assert.True(reloaded.Settings.LaunchAtStartup);
        Assert.Equal(0.3, reloaded.Settings.DimOpacity);
        Assert.False(reloaded.Settings.ShowShortcutHints);
        Assert.Equal("#FF4E8DF7", reloaded.Settings.AccentColor);
        Assert.Equal(Theme.Light, reloaded.Settings.Theme);
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
