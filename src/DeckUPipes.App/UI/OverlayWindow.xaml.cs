using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DeckUPipes.App.Services;
using DeckUPipes.Core;
using DeckUPipes.Interop;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;

namespace DeckUPipes.App.UI;

/// <summary>
/// The always-on-top overlay: a main panel with the header + SYSTEM row, and
/// floating per-app boxes below it. Focus model: SYSTEM is app index -1, apps
/// are 0..n-1; Tab/click/wheel cycle the focus and the TAB chip marks the next
/// target (see CycleLogic).
/// </summary>
public partial class OverlayWindow : Window
{
    private const double PanelWidth = 840;
    private const double GlobalRowHeight = 144;
    private const double ProgramRowHeight = 90;
    private const double ConnectorTopLead = 30;
    private const double ConnectorBottomLead = 18;
    private const double TerminationHeight = 42;
    private const double VerticalMargin = 24;
    private const double ScrollChromeHeight = 110;
    private const double WheelVolumeStep = 0.03;
    private const double VolumeLaneWidth = 520;
    private const double DragVolumePerPixel = 1.0 / (VolumeLaneWidth * 2.0);
    private const double KeyVolumeStep = 0.05;
    private const double FineVolumeStep = 0.01;

    private readonly AppSettings _settings;
    private readonly AudioSessionService _audioService;
    private readonly SystemAudioController _systemAudio;

    private readonly List<SessionViewModel> _viewModels = new();
    private bool _hasOpenedOnce;
    private int _focusedIndex = CycleLogic.SystemAppIndex;
    private bool _repositioning;
    private SkinAssetLoader? _skinAssets;
    private double _systemVolume;
    private bool _systemMuted;

    // Drag state (left-button drag on a bar adjusts volume horizontally).
    private SessionViewModel? _dragSession;
    private int _dragStartIndex;
    private bool _systemDragging;
    private Point _dragStart;
    private double _dragStartVolume;
    private bool _dragMoved;

    public OverlayWindow(AppSettings settings, AudioSessionService audioService, SystemAudioController systemAudio)
    {
        InitializeComponent();
        _settings = settings;
        _audioService = audioService;
        _systemAudio = systemAudio;

        Resources["DimOpacity"] = settings.DimOpacity;
        SessionScroll.MaxHeight = 0;
        EmptyState.Visibility = Visibility.Visible;

        // The overlay is always a vertical column anchored to the top edge, so
        // re-anchor whenever its size changes while visible.
        SizeChanged += (_, _) =>
        {
            if (IsVisible && !_repositioning)
            {
                Reposition();
            }
        };
    }

    public void ApplySkin(SkinAssetLoader skinAssets)
    {
        _skinAssets = skinAssets;
        MasterAvatarImage.Source = skinAssets.Get("avatar");
        MasterMuteImage.Source = skinAssets.Get("globalMute");
        MasterMuteImage.Visibility = MasterMuteImage.Source is null ? Visibility.Collapsed : Visibility.Visible;
        MasterMuteFallback.Visibility = MasterMuteImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
        ApplySkinResources();
        ApplySkinToRows();
        UpdateSystemSegments();
    }

    private void ApplySkinResources()
    {
        var appResources = System.Windows.Application.Current.Resources;
        Resources["SkinGlobalBackgroundBrush"] = _skinAssets?.GetTileBrush("globalBackground") ?? appResources["PanelBrush"];
        Resources["SkinProgramBackgroundBrush"] = _skinAssets?.GetTileBrush("programBackground") ?? appResources["AppBoxBrush"];
        Resources["SkinConnectorBrush"] = _skinAssets?.GetTileBrush("connector") ?? appResources["PanelBrush"];
        Resources["SkinTerminationBrush"] = _skinAssets?.GetTileBrush("termination") ?? appResources["PanelBrush"];
        Resources["SkinVolumeBackgroundBrush"] = _skinAssets?.GetTileBrush("volumeBackground") ?? appResources["TrackBrush"];
        Resources["SkinVolumeSegmentBrush"] = _skinAssets?.Get("globalVolume") is { } globalSegment
            ? new ImageBrush(globalSegment) { Stretch = Stretch.Fill }
            : appResources["AccentBrush"];
        Resources["SkinProgramMuteImage"] = _skinAssets?.Get("programMute") ?? LogoService.GetBitmapSource();
    }

    private void ApplySkinToRows()
    {
        var icon = _skinAssets?.Get("programIcon");
        var segment = _skinAssets?.Get("programVolume");
        var mute = _skinAssets?.Get("programMute");
        foreach (var vm in _viewModels)
        {
            vm.ApplySkinAssets(icon, segment, mute);
        }
    }

    private void UpdateSystemSegments()
    {
        var segment = _skinAssets?.Get("globalVolume");
        MasterVolumeSegments.ItemsSource = Enumerable.Range(0, 10)
            .Select(index => new VolumeSegmentState(
                index < Math.Ceiling(_systemVolume * 10) ? (_systemMuted ? 0.45 : 1.0) : 0.0,
                segment))
            .ToList();
    }

    /// <summary>Applies settings that affect the overlay (opacities, hints, position, theme brushes).</summary>
    public void ApplySettings()
    {
        Resources["DimOpacity"] = _settings.DimOpacity;
        CloseHint.Text = $"close with {_settings.Hotkey}";
        Reposition();
    }

    /// <summary>
    /// Places the overlay on the chosen monitor position. The work area from
    /// GetMonitorInfo is in PHYSICAL pixels while Window.Left/Top are DIPs, so
    /// it is converted first (otherwise the right/bottom edges drift off-screen
    /// on any display scaled above 100%).
    /// </summary>
    public void Reposition()
    {
        var work = _settings.MonitorMode == MonitorSelection.Fixed
            ? MonitorHelper.GetWorkAreaAt(_settings.MonitorIndex)
            : WorkAreaHelper.GetMonitorWorkAreaAtCursor();

        if (work.Width <= 0 || work.Height <= 0)
        {
            work = WorkAreaHelper.GetMonitorWorkAreaAtCursor();
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var workDip = new Rect(
            work.Left / dpi.DpiScaleX,
            work.Top / dpi.DpiScaleY,
            work.Width / dpi.DpiScaleX,
            work.Height / dpi.DpiScaleY);

        _repositioning = true;
        try
        {
            // Keep one vertical skin composition; Side only chooses its screen anchor.
            var offset = _settings.EdgeOffset;
            Width = PanelWidth;
            SessionScroll.MaxHeight = Math.Max(120, workDip.Height - (2 * VerticalMargin) - ScrollChromeHeight);
            UpdateLayout();

            var rightAnchor = _settings.Side == OverlaySide.TopRight;
            Left = rightAnchor ? workDip.Right - Width - offset : workDip.Left + offset;
            Top = workDip.Top + VerticalMargin;

            Opacity = _settings.FocusedOpacity;
            UpdateLayout();
        }
        finally
        {
            _repositioning = false;
        }
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        ShowMixer();
    }

    private void ShowMixer()
    {
        if (IsVisible)
        {
            return;
        }

        Reposition();
        Show();
        Activate();
        // Plain Activate() does not focus layered windows; force the
        // foreground explicitly so Tab/arrows/Esc reach the overlay.
        FocusHelper.Activate(new WindowInteropHelper(this).Handle);
        Keyboard.Focus(this);
        AnimateIn();
    }

    private void AnimateIn()
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(225));
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation(0.0, Opacity, duration) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = _settings.FocusedOpacity;
        };
        BeginAnimation(OpacityProperty, fade);

        // Slide in from the anchored edge without changing the layout.
        var slideFromX = _settings.Side == OverlaySide.TopRight ? -18.0 : 18.0;
        var slide = new DoubleAnimation(slideFromX, 0.0, duration) { EasingFunction = ease };
        slide.Completed += (_, _) =>
        {
            OverlaySlide.BeginAnimation(TranslateTransform.XProperty, null);
            OverlaySlide.X = 0;
        };
        OverlaySlide.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    /// <summary>Rebuilds the view model list from the audio service.</summary>
    public void RefreshSessions(IReadOnlyList<AudioSession> sessions)
    {
        var previousFocusedId = _focusedIndex >= 0 && _focusedIndex < _viewModels.Count
            ? _viewModels[_focusedIndex].InstanceId
            : null;
        var wasSystemFocused = _focusedIndex == CycleLogic.SystemAppIndex;

        _viewModels.Clear();
        _viewModels.AddRange(sessions.Select(s => new SessionViewModel(s)));
        var programIcon = _skinAssets?.Get("programIcon");
        var programSegment = _skinAssets?.Get("programVolume");
        var programMute = _skinAssets?.Get("programMute");
        foreach (var vm in _viewModels)
        {
            vm.ApplySkinAssets(programIcon, programSegment, programMute);
        }

        // Keep the focus on the same session (or fall back to the first app).
        int newIndex;
        if (!_hasOpenedOnce)
        {
            _hasOpenedOnce = true;
            newIndex = _viewModels.Count > 0 ? 0 : CycleLogic.SystemAppIndex;
        }
        else if (wasSystemFocused || _viewModels.Count == 0)
        {
            newIndex = CycleLogic.SystemAppIndex;
        }
        else if (previousFocusedId is not null)
        {
            var match = _viewModels.FindIndex(v => v.InstanceId == previousFocusedId);
            newIndex = match >= 0 ? match : 0;
        }
        else
        {
            newIndex = 0;
        }

        // IMPORTANT: assign a NEW list instance. WPF ignores an ItemsSource
        // assignment when the reference is unchanged, and List<T> raises no
        // change events — the UI would stay bound to stale view models and
        // focus/chip updates would never appear.
        SessionList.ItemsSource = new List<SessionViewModel>(_viewModels);

        var empty = _viewModels.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        SessionList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        UpdateProgramStackGeometry();

        ApplyFocus(newIndex);
    }

    private void UpdateProgramStackGeometry()
    {
        var rowCount = _viewModels.Count;
        var connectorHeight = ConnectorTopLead + (rowCount * ProgramRowHeight) + ConnectorBottomLead;
        ConnectorSurface.Height = connectorHeight;
        TerminationSurface.Visibility = rowCount == 0 ? Visibility.Collapsed : Visibility.Visible;
        ProgramStackSurface.UpdateLayout();
    }

    public void UpdateSystemVolume(double volume, bool isMuted, float peak)
    {
        _systemVolume = Math.Clamp(volume, 0.0, 1.0);
        _systemMuted = isMuted;
        var labelBrush = isMuted
            ? (Brush)FindResource("MuteBrush")
            : (Brush)FindResource("DimTextBrush");

        MasterVolumeText.Text = $"{Math.Round(volume * 100)}%";
        MasterLabel.Foreground = labelBrush;
        MasterMuteFallback.Opacity = isMuted ? 0.65 : 0.35;
        UpdateSystemSegments();
    }

    /// <summary>
    /// Verifies the overlay hides and reopens cleanly: a closed WPF window
    /// cannot be shown again, so reopening it from the tray or the hotkey must
    /// not crash. Used by the smoke test.
    /// </summary>
    public bool SelfTestVisibilityCycle()
    {
        Hide();
        var hidden = !IsVisible;
        ShowMixer();
        return hidden && IsVisible;
    }

    /// <summary>
    /// Verifies the SYSTEM row accepts mouse input: it must be hit-testable, or
    /// its wheel/drag/click handlers never run. Used by the smoke test.
    /// </summary>
    public bool SelfTestSystemRowInput()
    {
        var centre = SystemRowInline.TransformToAncestor(this).Transform(new Point(420, 72));
        return InputHitTest(centre) is DependencyObject node && IsWithin(node, SystemRowInline);
    }

    private static bool IsWithin(DependencyObject node, DependencyObject ancestor)
    {
        var current = node;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// Verifies the overlay geometry for a top-right placement: anchored to the
    /// top edge, using the fixed vertical-column width, and on-screen. Used by
    /// the smoke test.
    /// </summary>
    public bool SelfTestDockGeometry()
    {
        var original = _settings.Side;
        try
        {
            _settings.Side = OverlaySide.TopRight;
            Reposition();
            // Vertical column: fixed width, anchored to the top edge so its
            // height stays small relative to a full-height dock.
            var columnWidth = Math.Abs(ActualWidth - PanelWidth) < 1;
            var compact = ActualHeight < SystemParameters.WorkArea.Height;
            var onScreen = Left >= 0 && Top >= 0;
            return columnWidth && compact && onScreen;
        }
        finally
        {
            _settings.Side = original;
            Reposition();
        }
    }

    /// <summary>
    /// Verifies the SYSTEM row shows the mute color while the endpoint is muted.
    /// Used by the smoke test. Pumps the dispatcher so the audio poll's
    /// update can run (the test itself lives on the UI thread).
    /// </summary>
    public bool SelfTestSystemMuteVisual()
    {
        var normal = MasterMuteFallback.Opacity;
        _systemAudio.SetMute(true);
        try
        {
            var deadline = Environment.TickCount64 + 3000;
            while (Environment.TickCount64 < deadline)
            {
                if (MasterMuteFallback.Opacity != normal)
                {
                    return true;
                }

                var frame = new DispatcherFrame();
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            return false;
        }
        finally
        {
            _systemAudio.SetMute(false);
        }
    }

    private void ApplyFocus(int index)
    {
        _focusedIndex = index;
        for (var i = 0; i < _viewModels.Count; i++)
        {
            _viewModels[i].IsFocused = i == index;
        }

        // The panel itself is the SYSTEM "row": highlight it when focused.
        var systemFocused = index == CycleLogic.SystemAppIndex;
        var accentBrush = (Brush)FindResource("AccentBrush");
        var panelBrush = (Brush)FindResource("PanelBorderBrush");
        var dimBrush = (Brush)FindResource("DimTextBrush");
        SystemPanelBorder.BorderBrush = systemFocused ? accentBrush : panelBrush;
        MasterLabel.Foreground = systemFocused ? accentBrush : dimBrush;

        // Brief side-to-side "settle" on whichever element just gained focus.
        if (systemFocused)
        {
            AnimateFocusShake(SystemShake);
        }
        else if (index >= 0 && index < _viewModels.Count)
        {
            var container = SessionList.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
            var shake = container?.FindName("FocusShake") as TranslateTransform;
            if (shake is not null)
            {
                AnimateFocusShake(shake);
            }
        }

    }

    /// <summary>Brief side-to-side "settle" animation when something gains focus.</summary>
    private static void AnimateFocusShake(TranslateTransform transform)
    {
        var storyboard = new Storyboard { Duration = TimeSpan.FromMilliseconds(500) };
        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, new PropertyPath(TranslateTransform.XProperty));
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void CycleFocus(int direction)
    {
        var next = CycleLogic.NextAppIndex(_focusedIndex, _viewModels.Count, direction);
        ApplyFocus(next);
    }

    private static SessionViewModel? HitSession(DependencyObject? source)
    {
        var current = source;
        while (current is not null && current is not Window)
        {
            if (current is FrameworkElement { DataContext: SessionViewModel vm })
            {
                return vm;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    // ---- Mouse interaction ----

    // Wheel over a bar adjusts that app's volume; rows mark the event handled, so
    // this window-level handler only fires over empty space, where it cycles focus.
    private void OnOverlayMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // The overlay is a vertical column; wheel over empty space cycles focus.
        CycleFocus(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void OnSessionListMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var session = HitSession(e.OriginalSource as DependencyObject);
        if (session is null)
        {
            return;
        }

        var delta = e.Delta > 0 ? WheelVolumeStep : -WheelVolumeStep;
        _audioService.SetVolume(session.Session, Math.Clamp(session.Session.Volume + delta, 0.0, 1.0));
        e.Handled = true;
    }

    private void OnSystemMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = e.Delta > 0 ? WheelVolumeStep : -WheelVolumeStep;
        _systemAudio.SetVolume(Math.Clamp(_systemAudio.Volume + delta, 0.0, 1.0));
        e.Handled = true;
    }

    private void OnSessionListMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount > 1)
        {
            return;
        }

        var session = HitSession(e.OriginalSource as DependencyObject);
        if (session is null)
        {
            return;
        }

        var index = _viewModels.IndexOf(session);
        if (index < 0)
        {
            return;
        }

        _dragSession = session;
        _dragStartIndex = index;
        _systemDragging = false;
        _dragStart = e.GetPosition(this);
        _dragStartVolume = session.Session.Volume;
        _dragMoved = false;
        CaptureMouse();
    }

    private void OnSystemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount > 1)
        {
            return;
        }

        _dragSession = null;
        _systemDragging = true;
        _dragStart = e.GetPosition(this);
        _dragStartVolume = _systemAudio.Volume;
        _dragMoved = false;
        CaptureMouse();
    }

    private void OnWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured)
        {
            return;
        }

        var deltaX = e.GetPosition(this).X - _dragStart.X;
        if (Math.Abs(deltaX) > 3)
        {
            _dragMoved = true;
        }

        if (!_dragMoved)
        {
            return;
        }

        if (_dragSession is not null)
        {
            var volume = _dragStartVolume + (deltaX * DragVolumePerPixel);
            _audioService.SetVolume(_dragSession.Session, Math.Clamp(volume, 0.0, 1.0));
        }
        else if (_systemDragging)
        {
            _systemAudio.SetVolume(Math.Clamp(_dragStartVolume + (deltaX * DragVolumePerPixel), 0.0, 1.0));
        }
    }

    private void OnWindowMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !IsMouseCaptured)
        {
            return;
        }

        var session = _dragSession;
        var dragIndex = _dragStartIndex;
        var wasSystem = _systemDragging;
        var wasClick = !_dragMoved;
        _dragSession = null;
        _systemDragging = false;
        ReleaseMouseCapture();

        if (!wasClick)
        {
            return;
        }

        // A plain click: mute the focused box, focus any other box, mute SYSTEM.
        if (session is not null)
        {
            if (_focusedIndex == dragIndex)
            {
                _audioService.ToggleMute(session.Session);
            }
            else
            {
                ApplyFocus(dragIndex);
            }
        }
        else if (wasSystem)
        {
            _systemAudio.SetMute(!_systemAudio.IsMuted);
        }
    }

    // ---- Keyboard interaction (works when the overlay has focus) ----
    // Up/Down cycle the focus, Left/Right adjust volume, Tab also cycles.

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                CycleFocus(-1);
                break;
            case Key.Down:
                CycleFocus(1);
                break;
            case Key.Left:
                AdjustFocusedVolume(-KeyVolumeStep);
                break;
            case Key.Right:
                AdjustFocusedVolume(KeyVolumeStep);
                break;
            case Key.PageUp:
                AdjustFocusedVolume(FineVolumeStep);
                break;
            case Key.PageDown:
                AdjustFocusedVolume(-FineVolumeStep);
                break;
            case Key.M:
            case Key.Space:
                ToggleFocusedMute();
                break;
            case Key.Tab:
                CycleFocus(e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
                break;
            case Key.Escape:
                Hide();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void AdjustFocusedVolume(double delta)
    {
        if (_focusedIndex == CycleLogic.SystemAppIndex)
        {
            _systemAudio.SetVolume(Math.Clamp(_systemAudio.Volume + delta, 0.0, 1.0));
            if (_systemAudio.IsMuted)
            {
                _systemAudio.SetMute(false);
            }

            return;
        }

        if (_focusedIndex < 0 || _focusedIndex >= _viewModels.Count)
        {
            return;
        }

        var session = _viewModels[_focusedIndex].Session;
        _audioService.SetVolume(session, Math.Clamp(session.Volume + delta, 0.0, 1.0));
        if (session.IsMuted)
        {
            _audioService.SetMute(session, false);
        }
    }

    private void ToggleFocusedMute()
    {
        if (_focusedIndex == CycleLogic.SystemAppIndex)
        {
            _systemAudio.SetMute(!_systemAudio.IsMuted);
            return;
        }

        if (_focusedIndex >= 0 && _focusedIndex < _viewModels.Count)
        {
            _audioService.ToggleMute(_viewModels[_focusedIndex].Session);
        }
    }
}

