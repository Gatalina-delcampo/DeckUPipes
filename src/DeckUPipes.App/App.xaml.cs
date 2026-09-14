using System.Windows;
using System.Windows.Threading;
using DeckUPipes.App.Services;
using DeckUPipes.App.UI;
using DeckUPipes.Core;

namespace DeckUPipes.App;

public partial class App : System.Windows.Application
{
    private SettingsStore _settingsStore = null!;
    private HotkeyService? _hotkeyService;
    private string _registeredHotkeySpec = string.Empty;
    private bool _lastAutostart;
    private TrayIcon _trayIcon = null!;
    private OverlayWindow _overlayWindow = null!;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private readonly SkinCatalog _skinCatalog = new();
    private readonly SkinAssetLoader _skinAssetLoader = new();
    private AudioSessionService _audioService = null!;
    private SystemAudioController _systemAudio = null!;
    private Mutex? _singleInstanceMutex;
    private Mutex? _legacySingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Only one instance may own the global hotkey and tray icon.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\DeckUPipes.SingleInstance", out var createdNew);
        _legacySingleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\EarClarinet.SingleInstance", out var legacyCreatedNew);
        if (!createdNew || !legacyCreatedNew)
        {
            System.Windows.MessageBox.Show("DeckUPipes is already running.", "DeckUPipes", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        _settingsStore = new SettingsStore();
        _settingsStore.Load();
        ThemeManager.Apply(_settingsStore.Settings, Resources);
        LoadSkin(_settingsStore.Settings.SkinId);

        _trayIcon = new TrayIcon();
        _trayIcon.ToggleMixerRequested += OnToggleMixerRequested;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.AboutRequested += OnAboutRequested;
        _trayIcon.ExitRequested += OnExitRequested;

        _audioService = new AudioSessionService();
        _systemAudio = new SystemAudioController();
        _overlayWindow = new OverlayWindow(_settingsStore.Settings, _audioService, _systemAudio);
        _overlayWindow.ApplySkin(_skinAssetLoader);

        try
        {
            _audioService.SessionsChanged += OnSessionsChanged;
            _audioService.Start();
            _systemAudio.Start();
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloon("DeckUPipes", $"Audio initialization failed: {ex.Message}");
        }

        ReRegisterHotkey();

        _systemAudio.StateChanged += (_, _) => Dispatcher.Invoke(UpdateSystemOverlay);
        _settingsStore.SettingsChanged += OnSettingsChanged;

        _registeredHotkeySpec = _settingsStore.Settings.Hotkey;
        _lastAutostart = _settingsStore.Settings.LaunchAtStartup;

        // First run: tell the user the app is alive and guide them to the
        // configuration (hotkey, position, theme).
        var smoke = e.Args.Any(a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));
        if (_settingsStore.IsFirstRun && !smoke)
        {
            _trayIcon.ShowBalloon("DeckUPipes is running", "Configure your hotkey and preferences.");
            OnSettingsRequested();
        }

        _overlayWindow.Reposition();

        if (smoke)
        {
            RunSmokeTest();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        ThemeManager.Apply(_settingsStore.Settings, Resources);
        LoadSkin(_settingsStore.Settings.SkinId);
        _overlayWindow.ApplySettings();

        var settings = _settingsStore.Settings;
        if (!string.Equals(_registeredHotkeySpec, settings.Hotkey, StringComparison.Ordinal))
        {
            ReRegisterHotkey();
        }

        if (_lastAutostart != settings.LaunchAtStartup)
        {
            _lastAutostart = settings.LaunchAtStartup;
            try
            {
                AutoStartService.SetEnabled(_lastAutostart);
            }
            catch (Exception ex)
            {
                _trayIcon.ShowBalloon("DeckUPipes", $"Could not update auto-start: {ex.Message}");
            }
        }
    }

    private void LoadSkin(string? skinId)
    {
        var package = string.IsNullOrWhiteSpace(skinId)
            ? null
            : _skinCatalog.Discover().FirstOrDefault(item => string.Equals(item.Manifest.Id, skinId, StringComparison.OrdinalIgnoreCase));
        _skinAssetLoader.Load(package);
        if (_overlayWindow is not null)
        {
            _overlayWindow.ApplySkin(_skinAssetLoader);
        }
    }

    private void ReRegisterHotkey()
    {
        _hotkeyService?.Dispose();
        _hotkeyService = null;

        var spec = _settingsStore.Settings.Hotkey;
        _registeredHotkeySpec = spec;

        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            _hotkeyService = new HotkeyService(spec);
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloon("DeckUPipes", $"Could not register hotkey '{spec}': {ex.Message}");
        }
    }

    private void OnSessionsChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(() => _overlayWindow.RefreshSessions(_audioService.Sessions));

    private void UpdateSystemOverlay() =>
        _overlayWindow.UpdateSystemVolume(_systemAudio.Volume, _systemAudio.IsMuted, _systemAudio.Peak);

    private void RunSmokeTest()
    {
        var results = new List<(string Name, bool Passed)>();

        _overlayWindow.Show();

        // Self-test: the overlay must hide and reopen cleanly.
        results.Add(("VisibilityCycle", _overlayWindow.SelfTestVisibilityCycle()));
        _overlayWindow.Show();

        // Self-test: dock geometry, SYSTEM mute visual, SYSTEM row input.
        results.Add(("DockGeometry", _overlayWindow.SelfTestDockGeometry()));
        results.Add(("SystemMuteVisual", _overlayWindow.SelfTestSystemMuteVisual()));
        results.Add(("SystemRowInput", _overlayWindow.SelfTestSystemRowInput()));

        foreach (var (name, passed) in results)
        {
            Console.WriteLine($"{name} self-test: {(passed ? "PASS" : "FAIL")}");
        }

        var failed = results.Count(result => !result.Passed);
        Console.WriteLine($"self-tests: {results.Count - failed}/{results.Count} passed");

        // Exercise open -> close -> reopen (a closed WPF window cannot be
        // shown again, and reopening from the tray must not crash).
        OnSettingsRequested();
        _settingsWindow?.Close();
        OnSettingsRequested();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            await Task.Run(PrintSessionReport);
            Shutdown(failed == 0 ? 0 : 1);
        };
        timer.Start();
    }

    private void PrintSessionReport()
    {
        if (_audioService is null)
        {
            Console.WriteLine("audio service unavailable");
            return;
        }

        var target = _audioService.Sessions.FirstOrDefault(s => s.ProcessId > 0 && !s.IsSystemSounds);
        if (target is not null)
        {
            var original = target.Volume;
            var originalMute = target.IsMuted;
            _audioService.SetVolume(target, 0.4);
            Thread.Sleep(400);
            Console.WriteLine($"ControlTest: SetVolume(0.4) -> read back {target.Volume:P0} ({(Math.Abs(target.Volume - 0.4) < 0.02 ? "PASS" : "FAIL")})");
            _audioService.SetMute(target, true);
            Thread.Sleep(400);
            Console.WriteLine($"ControlTest: SetMute(true) -> read back mute={target.IsMuted} ({(target.IsMuted ? "PASS" : "FAIL")})");
            _audioService.SetMute(target, false);
            _audioService.SetVolume(target, original);
            if (originalMute)
            {
                _audioService.SetMute(target, true);
            }

            Thread.Sleep(300);
        }
        else
        {
            Console.WriteLine("ControlTest: no controllable session found (skipped)");
        }

        Console.WriteLine($"Sessions: {_audioService.Sessions.Count}");
        foreach (var session in _audioService.Sessions)
        {
            Console.WriteLine(
                $"{session.DisplayName} | pid={session.ProcessId} | exe={session.ProcessName} | " +
                $"vol={session.Volume:P0} | mute={session.IsMuted} | active={session.IsActive} | system={session.IsSystemSounds}");
        }

        if (_systemAudio is not null)
        {
            Console.WriteLine($"System | vol={_systemAudio.Volume:P0} | mute={_systemAudio.IsMuted} | peak={_systemAudio.Peak:P2}");
        }
    }

    private void OnHotkeyPressed() => Dispatcher.Invoke(_overlayWindow.Toggle);

    private void OnToggleMixerRequested() => _overlayWindow.Toggle();

    private void OnSettingsRequested()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settingsStore);
            // A closed WPF window cannot be shown again; drop the reference so
            // the next request creates a fresh one.
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnAboutRequested()
    {
        if (_aboutWindow is null)
        {
            _aboutWindow = new AboutWindow();
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        }

        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    private void OnExitRequested()
    {
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _audioService?.Dispose();
        _systemAudio?.Dispose();
        // Null when startup bailed out early (e.g. second instance).
        if (_settingsStore is not null)
        {
            _settingsStore.Save();
        }

        base.OnExit(e);
    }
}


