using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using DeckUPipes.App.Services;
using DeckUPipes.Core;
using DeckUPipes.Interop;
using Brush = System.Windows.Media.Brush;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using RadioButton = System.Windows.Controls.RadioButton;

namespace DeckUPipes.App.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private bool _loading;
    private bool _capturingHotkey;
    private bool _loadingSkin;
    private SkinInstallCandidate? _skinCandidate;
    private Action? _skinCardAction;

    private readonly string _skinStagingRoot = Path.Combine(Path.GetTempPath(), "DeckUPipes");

    private sealed record SkinChoice(string Id, string DisplayName, SkinPackage? Package);

    private readonly SkinCatalog _skinCatalog = new();

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
        LoadSkins();
        LoadFromSettings();

        // A pending import owns a staging folder that must not outlive the window.
        Closed += (_, _) => CancelSkinImport();
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

    private void LoadSkins()
    {
        var choices = new List<SkinChoice> { new("", "Default theme", null) };
        choices.AddRange(_skinCatalog.Discover().Select(package =>
            new SkinChoice(package.Manifest.Id, package.Manifest.Id, package)));
        SkinCombo.ItemsSource = choices;
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
            // Reflect what Windows will actually do, not just the stored preference.
            LaunchAtStartupCheck.IsChecked = AutoStartService.IsEnabled();
            ModernUiRadio.IsChecked = settings.UiMode == UiMode.Modern;
            LegacyUiRadio.IsChecked = settings.UiMode == UiMode.Legacy;

            DarkThemeRadio.IsChecked = settings.Theme == Theme.Dark;
            LightThemeRadio.IsChecked = settings.Theme == Theme.Light;
            CheckAccentSwatch(settings.AccentColor);
            _loadingSkin = true;
            SkinCombo.SelectedValue = settings.SkinId ?? string.Empty;
            _loadingSkin = false;
            UpdateRemoveButton();
            SkinStatus.Text = string.IsNullOrWhiteSpace(settings.SkinId)
                ? "Using the built-in theme."
                : SkinCombo.SelectedItem is SkinChoice choice && choice.Package is not null && choice.Package.Validation.IsValid
                    ? "Skin loaded and validated."
                    : "Selected skin is unavailable; the built-in theme is active.";
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

    private void OnSkinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _loadingSkin || SkinCombo.SelectedValue is not string skinId)
        {
            return;
        }

        _store.Update(settings => settings.SkinId = string.IsNullOrWhiteSpace(skinId) ? null : skinId);
        UpdateRemoveButton();
        SkinStatus.Text = string.IsNullOrWhiteSpace(skinId)
            ? "Using the built-in theme."
            : "Skin selection saved. The overlay will reload it.";
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

    /// <summary>Checks the position option matching the stored side.</summary>
    private void CheckSidePicker(OverlaySide side)
    {
        var radios = new[]
        {
            SideTopLeft, SideTopRight,
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

    private void OnUiModeChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.UiMode = LegacyUiRadio.IsChecked == true ? UiMode.Legacy : UiMode.Modern);
    }

    private void OnLaunchAtStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _store.Update(s => s.LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true);
    }

    // ---- Skin import and removal ----
    // Nothing is written to the skins folder until the review card is confirmed, and
    // the folder the user picked is only ever read from.

    private void OnLoadSkinClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load skin package",
            Filter = "Skin package (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            BeginSkinInspect(dialog.FileName);
        }
    }

    private void OnLoadFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a skin folder",
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == true)
        {
            BeginSkinInspect(dialog.FolderName);
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedSkin(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedSkin(e, out var path))
        {
            BeginSkinInspect(path);
        }

        e.Handled = true;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SkinCard.Visibility == Visibility.Visible)
        {
            CancelSkinImport();
            e.Handled = true;
        }
    }

    /// <summary>Accepts a single dropped folder or .zip package.</summary>
    private static bool TryGetDroppedSkin(DragEventArgs e, out string path)
    {
        path = string.Empty;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } dropped)
        {
            return false;
        }

        var candidate = dropped[0];
        var isArchive = File.Exists(candidate) &&
                        string.Equals(Path.GetExtension(candidate), ".zip", StringComparison.OrdinalIgnoreCase);
        if (!Directory.Exists(candidate) && !isArchive)
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private void BeginSkinInspect(string path)
    {
        CancelSkinImport();
        SetSkinBusy(true, $"Reading {Path.GetFileName(path)}...");
        try
        {
            _skinCandidate = SkinInstaller.Inspect(path, _skinStagingRoot);
            ShowSkinCandidate();
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            ShowSkinError(Path.GetFileName(path), exception.Message);
        }
        finally
        {
            SetSkinBusy(false, null);
        }
    }

    private void ShowSkinCandidate()
    {
        var candidate = _skinCandidate;
        if (candidate is null)
        {
            return;
        }

        var sourceName = Path.GetFileName(candidate.SourcePath);
        if (candidate.Manifest is null)
        {
            ShowSkinError(sourceName, candidate.Validation.Errors.FirstOrDefault() ?? "The skin could not be read.");
            return;
        }

        var id = candidate.Manifest.Id;
        var detail = $"id: {id} - {candidate.Manifest.Slots.Count} objects";
        var issues = string.Join(
            Environment.NewLine,
            candidate.Validation.Errors
                .Select(error => "- " + error)
                .Concat(candidate.Validation.Warnings.Select(warning => "- " + warning)));

        if (!candidate.IsValid)
        {
            ShowSkinCard(
                sourceName,
                $"{detail} - this package cannot be installed.",
                issues + Environment.NewLine + "Nothing was copied. Fix these and try again.",
                hasErrors: true,
                primaryLabel: null,
                primaryAction: null);
            return;
        }

        var replaces = SkinInstaller.IsInstalled(_skinCatalog.RootDirectory, id);
        var note = replaces
            ? "A skin with this id is already installed and will be replaced."
            : _skinCatalog.Discover().Any(package => string.Equals(package.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
                ? "A skin with this id already exists elsewhere and the new copy will take priority."
                : "The overlay will switch to it immediately.";

        ShowSkinCard(
            sourceName,
            $"{detail} - all required objects validated.",
            string.IsNullOrEmpty(issues) ? note : issues + Environment.NewLine + note,
            hasErrors: false,
            primaryLabel: replaces ? $"Replace \"{id}\"" : "Install skin",
            primaryAction: () => InstallSkinCandidate(candidate));
    }

    private void InstallSkinCandidate(SkinInstallCandidate candidate)
    {
        var id = candidate.Manifest!.Id;
        SetSkinBusy(true, $"Installing {id}...");
        try
        {
            SkinInstaller.Commit(candidate, _skinCatalog.RootDirectory);
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            SetSkinBusy(false, null);
            ShowSkinError(Path.GetFileName(candidate.SourcePath), exception.Message);
            return;
        }

        CancelSkinImport();
        SetSkinBusy(false, null);
        RefreshSkinChoices(id);
        _store.Update(settings => settings.SkinId = id);
        SkinStatus.Text = $"Installed \"{id}\" - the overlay is using it now.";
    }

    private void OnRemoveSkinClicked(object sender, RoutedEventArgs e)
    {
        var choice = SkinCombo.SelectedItem as SkinChoice;
        var package = choice?.Package;
        if (choice is null || package is null)
        {
            return;
        }

        ShowSkinCard(
            $"Remove \"{choice.Id}\"?",
            package.DirectoryPath + Environment.NewLine +
            "This deletes the folder. The built-in theme is used if this skin was selected.",
            string.Empty,
            hasErrors: false,
            primaryLabel: "Remove skin",
            primaryAction: () => RemoveSkin(choice.Id, package.DirectoryPath));
    }

    private void RemoveSkin(string skinId, string directory)
    {
        try
        {
            SkinInstaller.Remove(directory, new[] { _skinCatalog.RootDirectory, _skinCatalog.LegacyRootDirectory });
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            ShowSkinError(Path.GetFileName(directory), exception.Message);
            return;
        }

        var wasSelected = string.Equals(_store.Settings.SkinId, skinId, StringComparison.OrdinalIgnoreCase);
        CancelSkinImport();
        RefreshSkinChoices(wasSelected ? string.Empty : _store.Settings.SkinId);
        if (wasSelected)
        {
            _store.Update(settings => settings.SkinId = null);
        }

        SkinStatus.Text = $"Removed \"{skinId}\".";
    }

    private void OnSkinCardPrimaryClicked(object sender, RoutedEventArgs e) => _skinCardAction?.Invoke();

    private void OnSkinCardCancelClicked(object sender, RoutedEventArgs e) => CancelSkinImport();

    private void CancelSkinImport()
    {
        _skinCandidate?.Dispose();
        _skinCandidate = null;
        _skinCardAction = null;
        SkinCard.Visibility = Visibility.Collapsed;
    }

    /// <summary>Reloads the dropdown and selects the wanted skin.</summary>
    private void RefreshSkinChoices(string? skinId)
    {
        _loadingSkin = true;
        LoadSkins();
        SkinCombo.SelectedValue = skinId ?? string.Empty;
        _loadingSkin = false;
        UpdateRemoveButton();
    }

    private void UpdateRemoveButton() =>
        RemoveSkinButton.IsEnabled = SkinCombo.SelectedItem is SkinChoice { Package: not null };

    private void SetSkinBusy(bool busy, string? message)
    {
        Cursor = busy ? Cursors.Wait : null;
        LoadSkinButton.IsEnabled = !busy;
        LoadFolderButton.IsEnabled = !busy;
        RemoveSkinButton.IsEnabled = !busy && SkinCombo.SelectedItem is SkinChoice { Package: not null };
        if (!string.IsNullOrEmpty(message))
        {
            SkinStatus.Text = message;
        }
    }

    private void ShowSkinCard(string title, string detail, string issues, bool hasErrors, string? primaryLabel, Action? primaryAction)
    {
        SkinCardTitle.Text = title;
        SkinCardDetail.Text = detail;
        SkinCardIssues.Text = issues;
        SkinCardIssues.Visibility = string.IsNullOrEmpty(issues) ? Visibility.Collapsed : Visibility.Visible;
        SkinCardIssues.Foreground = (Brush)FindResource(hasErrors ? "MuteBrush" : "DimTextBrush");
        SkinCardPrimary.Content = primaryLabel ?? string.Empty;
        SkinCardPrimary.Visibility = primaryLabel is null ? Visibility.Collapsed : Visibility.Visible;
        SkinCardCancel.Content = primaryLabel is null ? "Close" : "Cancel";
        _skinCardAction = primaryAction;
        SkinCard.Visibility = Visibility.Visible;

        if (primaryLabel is not null)
        {
            SkinCardPrimary.Focus();
        }
    }

    private void ShowSkinError(string title, string message) =>
        ShowSkinCard(title, message, string.Empty, hasErrors: true, primaryLabel: null, primaryAction: null);

    private static bool IsFileSystemFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException
            or ArgumentException;

}

