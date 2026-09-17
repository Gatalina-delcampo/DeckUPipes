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
/// The original EarClarinet overlay, kept as an optional look. A 340px column with
/// a compact header, an inline SYSTEM row whose volume is a continuous fill with a
/// peak bar behind it, and app rows built the same way. It ignores skins - the
/// built-in theme and the accent colour are all it uses - and it anchors to the
/// top-left or top-right only, since the old horizontal dock is gone.
/// </summary>
public partial class LegacyOverlayWindow : Window, IOverlayHost
{
    private const double PanelWidth = 340;
    private const double VerticalMargin = 24;
    private const double ScrollChromeHeight = 110;
    private const double WheelVolumeStep = 0.03;
    private const double DragVolumePerPixel = 0.006;
    private const double KeyVolumeStep = 0.05;
    private const double FineVolumeStep = 0.01;

    private readonly AppSettings _settings;
    private readonly AudioSessionService _audioService;
    private readonly SystemAudioController _systemAudio;

    private readonly List<SessionViewModel> _viewModels = new();
    private bool _hasOpenedOnce;
    private int _focusedIndex = CycleLogic.SystemAppIndex;
    private bool _repositioning;

    // Drag state (left-button drag on a bar adjusts volume horizontally).
    private SessionViewModel? _dragSession;
    private int _dragStartIndex;
    private bool _systemDragging;
    private Point _dragStart;
    private double _dragStartVolume;
    private bool _dragMoved;

    public LegacyOverlayWindow(AppSettings settings, AudioSessionService audioService, SystemAudioController systemAudio)
    {
        InitializeComponent();
        _settings = settings;
        _audioService = audioService;
        _systemAudio = systemAudio;

        HeaderLogo.Source = LogoService.GetBitmapSource();
        HeaderLogo.Visibility = HeaderLogo.Source is null ? Visibility.Collapsed : Visibility.Visible;

        Resources["DimOpacity"] = settings.DimOpacity;
        SessionScroll.MaxHeight = 0;
        EmptyState.Visibility = Visibility.Visible;

        // The dock's height depends on content, so re-anchor whenever sessions
        // change the size while it is visible.
        SizeChanged += (_, _) =>
        {
            if (IsVisible && !_repositioning)
            {
                Reposition();
            }
        };
    }

    /// <summary>Applies settings that affect the overlay (opacities, hints, position, theme brushes).</summary>
    public void ApplySettings()
    {
        Resources["DimOpacity"] = _settings.DimOpacity;
        foreach (var vm in _viewModels)
        {
            vm.ShortcutHintsEnabled = true;
        }

        CloseHint.Text = $"close with {_settings.Hotkey}";
        Reposition();
        RecomputeHints();
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
            var offset = _settings.EdgeOffset;
            var rightAnchor = _settings.Side == OverlaySide.TopRight;

            Width = PanelWidth;
            SessionScroll.MaxHeight = Math.Max(120, workDip.Height - (2 * VerticalMargin) - ScrollChromeHeight);
            UpdateLayout();

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

        // Slide in from the anchored edge: sideways from the edge it hangs on, and
        // down from the top, as the original did.
        double slideFromX = _settings.Side == OverlaySide.TopRight ? -18 : 18;
        const double slideFromY = -18;

        if (slideFromY != 0)
        {
            var rise = new DoubleAnimation(slideFromY, 0.0, duration) { EasingFunction = ease };
            rise.Completed += (_, _) =>
            {
                OverlaySlide.BeginAnimation(TranslateTransform.YProperty, null);
                OverlaySlide.Y = 0;
            };
            OverlaySlide.BeginAnimation(TranslateTransform.YProperty, rise);
        }

        if (slideFromX != 0)
        {
            var slide = new DoubleAnimation(slideFromX, 0.0, duration) { EasingFunction = ease };
            slide.Completed += (_, _) =>
            {
                OverlaySlide.BeginAnimation(TranslateTransform.XProperty, null);
                OverlaySlide.X = 0;
            };
            OverlaySlide.BeginAnimation(TranslateTransform.XProperty, slide);
        }
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
        foreach (var vm in _viewModels)
        {
            vm.ShortcutHintsEnabled = true;
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

        ApplyFocus(newIndex);
    }

    public void UpdateSystemVolume(double volume, bool isMuted, float peak)
    {
        var volumeBrush = isMuted
            ? (Brush)FindResource("MuteBrush")
            : (Brush)FindResource("AccentBrush");
        var labelBrush = isMuted
            ? (Brush)FindResource("MuteBrush")
            : (Brush)FindResource("DimTextBrush");

        MasterVolumeScale.ScaleX = Math.Clamp(volume, 0.0, 1.0);
        MasterPeakScale.ScaleX = Math.Clamp(peak, 0.0, 1.0);
        MasterVolumeText.Text = $"{Math.Round(volume * 100)}%";
        MasterLabel.Foreground = labelBrush;
        // The SYSTEM row mutes too: show it like an app box would.
        MasterVolumeFill.Background = volumeBrush;
    }

    /// <summary>Raises a real mouse-down through the close button; used by the smoke test.</summary>
    public bool SelfTestCloseButton()
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseDownEvent,
            Source = CloseButton,
        };
        CloseButton.RaiseEvent(args);
        return !IsVisible;
    }

    /// <summary>
    /// Verifies the legacy column's geometry: the fixed width, a height that fits
    /// the screen, and an on-screen anchor. Mirrors the modern overlay's check.
    /// </summary>
    public bool SelfTestGeometry()
    {
        var original = _settings.Side;
        try
        {
            _settings.Side = OverlaySide.TopRight;
            Reposition();
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
        var normal = MasterVolumeFill.Background;
        _systemAudio.SetMute(true);
        try
        {
            var deadline = Environment.TickCount64 + 3000;
            while (Environment.TickCount64 < deadline)
            {
                if (!ReferenceEquals(MasterVolumeFill.Background, normal))
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

        RecomputeHints();
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

    /// <summary>Refreshes the TAB chips (app boxes + SYSTEM row) and ESC chips.</summary>
    private void RecomputeHints()
    {
        var appCount = _viewModels.Count;
        var tabTarget = CycleLogic.NextTabTargetAppIndex(_focusedIndex, appCount);
        var systemIsNext = tabTarget is null;

        SystemTabChip.Visibility = systemIsNext
            ? Visibility.Visible
            : Visibility.Collapsed;

        for (var i = 0; i < appCount; i++)
        {
            _viewModels[i].IsNextTabTarget = i == tabTarget;
        }
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

        CycleFocus(e.Delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void OnCloseClicked(object sender, MouseButtonEventArgs e)
    {
        Hide();
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
