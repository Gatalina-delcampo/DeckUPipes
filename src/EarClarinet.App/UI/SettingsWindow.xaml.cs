using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EarClarinet.App.Services;
using EarClarinet.Core;
using EarClarinet.Interop;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using RadioButton = System.Windows.Controls.RadioButton;

namespace EarClarinet.App.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private bool _loading;
    private bool _capturingHotkey;

    public SettingsWindow(SettingsStore store)
    {
        _store = store;
        _loading = true;
        InitializeComponent();

        HeaderLogo.Source = LogoService.GetBitmapSource();
        HeaderLogo.Visibility = HeaderLogo.Source is null ? Visibility.Collapsed : Visibility.Visible;
        Icon = LogoService.GetBitmapSource();
        VersionFooter.Text = $"{AppInfo.Name} v{AppInfo.Version} · {AppInfo.License}";

        LoadMonitors();
        LoadFromSettings();
    }

    private void LoadMonitors()
    {
        var monitors = MonitorHelper.GetMonitors();
        MonitorsCombo.ItemsSource = monitors
            .Select((m, index) => new ComboBoxItem
            {
                Content = $"Monitor {index + 1} ({m.WorkArea.Width} x {m.WorkArea.Height})",
                Tag = index,
            })
            .ToList();
    }

    private void LoadFromSettings()
    {
        _loading = true;
        try
        {
            var settings = _store.Settings;
            UpdateHotkeyDisplay(settings.Hotkey);
            CheckSidePicker(settings.Side);
            EdgeOffsetSlider.Value = settings.EdgeOffset;
            FocusedOpacitySlider.Value = settings.FocusedOpacity;
            DimOpacitySlider.Value = settings.DimOpacity;
            MonitorCursorRadio.IsChecked = settings.MonitorMode == MonitorSelection.Cursor;
            MonitorFixedRadio.IsChecked = settings.MonitorMode == MonitorSelection.Fixed;
            MonitorsCombo.SelectedIndex = MonitorsCombo.Items.Count > 0
                ? Math.Clamp(settings.MonitorIndex, 0, MonitorsCombo.Items.Count - 1)
                : -1;
            MonitorsCombo.IsEnabled = settings.MonitorMode == MonitorSelection.Fixed;
            LaunchAtStartupCheck.IsChecked = settings.LaunchAtStartup;
            ShowHintsCheck.IsChecked = settings.ShowShortcutHints;

            DarkThemeRadio.IsChecked = settings.Theme == Theme.Dark;
            LightThemeRadio.IsChecked = settings.Theme == Theme.Light;
            CheckAccentSwatch(settings.AccentColor);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Checks the swatch whose hex matches the stored accent color.</summary>
    private void CheckAccentSwatch(string hex)
    {
        var match = new[]
        {
            AccentTeal, AccentBlue, AccentPurple, AccentPink,
            AccentOrange, AccentGreen, AccentRed,
        }.FirstOrDefault(r => r.Tag is string tag &&
                              string.Equals(tag, hex, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            match.IsChecked = true;
        }
    }

    private void UpdateHotkeyDisplay(string spec)
    {
        HotkeyBox.Text = spec.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? "None (disabled)"
            : spec;
    }

    private void OnHotkeyBoxGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyHint.Text = "Press the key combination now. Esc to cancel.";
        HotkeyBox.SelectAll();
    }

    private void OnHotkeyBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _capturingHotkey = false;
        HotkeyHint.Text = "Click the box and press a key combination (e.g. Ctrl+Alt+M).";
    }

    private void OnHotkeyBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey)
        {
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            _capturingHotkey = false;
            UpdateHotkeyDisplay(_store.Settings.Hotkey);
            HotkeyHint.Text = "Cancelled.";
            return;
        }

        if (e.Key == Key.Tab)
        {
            _capturingHotkey = false;
            ClearHotkeyButton.Focus();
            return;
        }

        if (IsModifierKey(e.Key))
        {
            HotkeyHint.Text = "Press a key together with a modifier (Ctrl, Alt, Shift or Win).";
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var hasModifier = modifiers.HasFlag(ModifierKeys.Control)
            || modifiers.HasFlag(ModifierKeys.Alt)
            || modifiers.HasFlag(ModifierKeys.Shift)
            || modifiers.HasFlag(ModifierKeys.Windows);
        if (!hasModifier)
        {
            HotkeyHint.Text = "Add a modifier: Ctrl, Alt, Shift or Win.";
            return;
        }

        var spec = BuildSpec(modifiers, e.Key);
        try
        {
            HotkeyService.Parse(spec);
        }
        catch (Exception ex)
        {
            HotkeyHint.Text = ex.Message;
            return;
        }

        _capturingHotkey = false;
        _store.Update(s => s.Hotkey = spec);
        UpdateHotkeyDisplay(spec);
        HotkeyHint.Text = "Saved.";
    }

    private void OnClearHotkeyClicked(object sender, RoutedEventArgs e)
    {
        _store.Update(s => s.Hotkey = "None");
        UpdateHotkeyDisplay("None");
        HotkeyHint.Text = "Hotkey disabled.";
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;

    private static string BuildSpec(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Control");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.Theme = DarkThemeRadio.IsChecked == true ? Theme.Dark : Theme.Light);
    }

    private void OnAccentChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var swatch = sender as RadioButton;
        if (swatch?.Tag is string hex)
        {
            _store.Update(s => s.AccentColor = hex);
        }
    }

    /// <summary>Checks the position-picker dot matching the stored side.</summary>
    private void CheckSidePicker(OverlaySide side)
    {
        var radios = new[]
        {
            SideTopLeft, SideTopCenter, SideTopRight,
            SideLeft, SideRight,
            SideBottomLeft, SideBottomCenter, SideBottomRight,
        };

        var match = radios.FirstOrDefault(r => r.Tag is string tag &&
                                               string.Equals(tag, side.ToString(), StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            match.IsChecked = true;
        }
    }

    private void OnSideChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var tag = (sender as RadioButton)?.Tag as string;
        if (Enum.TryParse<OverlaySide>(tag, ignoreCase: true, out var side))
        {
            _store.Update(s => s.Side = side);
        }
    }

    private void OnEdgeOffsetChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.EdgeOffset = (int)e.NewValue);
    }

    private void OnFocusedOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.FocusedOpacity = e.NewValue);
    }

    private void OnDimOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.DimOpacity = e.NewValue);
    }

    private void OnMonitorModeChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        var mode = MonitorCursorRadio.IsChecked == true ? MonitorSelection.Cursor : MonitorSelection.Fixed;
        MonitorsCombo.IsEnabled = mode == MonitorSelection.Fixed;
        _store.Update(s =>
        {
            s.MonitorMode = mode;
            s.MonitorIndex = MonitorsCombo.SelectedIndex >= 0 ? MonitorsCombo.SelectedIndex : 0;
        });
    }

    private void OnMonitorsComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || MonitorsCombo.SelectedIndex < 0)
        {
            return;
        }

        _store.Update(s => s.MonitorIndex = MonitorsCombo.SelectedIndex);
    }

    private void OnLaunchAtStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true);
    }

    private void OnShowHintsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.ShowShortcutHints = ShowHintsCheck.IsChecked == true);
    }
}
