using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EarClarinet.App.Services;
using EarClarinet.Core;

namespace EarClarinet.App.UI;

/// <summary>
/// UI wrapper over an AudioSession. Keeps the volume/mute data flowing from
/// the audio service into the overlay via INotifyPropertyChanged.
/// </summary>
public sealed class SessionViewModel : INotifyPropertyChanged
{
    private readonly AudioSession _session;
    private bool _isFocused;
    private bool _isNextTabTarget;
    private bool _shortcutHintsEnabled;

    public SessionViewModel(AudioSession session)
    {
        _session = session;
        _session.PropertyChanged += OnSessionPropertyChanged;
        Icon = CreateImageSource();
    }

    public AudioSession Session => _session;

    public string InstanceId => _session.InstanceId;

    public string DisplayName => _session.DisplayName;

    public ImageSource? Icon { get; }

    public double Volume => _session.Volume;

    public bool IsMuted => _session.IsMuted;

    public string VolumeText => $"{Math.Round(_session.Volume * 100)}%";

    public bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused == value)
            {
                return;
            }

            _isFocused = value;
            RaisePropertyChanged(nameof(IsFocused));
            RaisePropertyChanged(nameof(IsMuteHint));
        }
    }

    /// True when this box is the next TAB target (shows the TAB chip).
    public bool IsNextTabTarget
    {
        get => _isNextTabTarget;
        set
        {
            if (_isNextTabTarget == value)
            {
                return;
            }

            _isNextTabTarget = value;
            RaisePropertyChanged(nameof(IsNextTabTarget));
        }
    }

    /// True when this box is focused and shortcut hints are enabled (shows the ESC chip).
    public bool IsMuteHint => _shortcutHintsEnabled && _isFocused;

    /// Mirror of the global "ShowShortcutHints" setting, pushed from the overlay.
    public bool ShortcutHintsEnabled
    {
        get => _shortcutHintsEnabled;
        set
        {
            if (_shortcutHintsEnabled == value)
            {
                return;
            }

            _shortcutHintsEnabled = value;
            RaisePropertyChanged(nameof(ShortcutHintsEnabled));
            RaisePropertyChanged(nameof(IsMuteHint));
        }
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AudioSession.Volume):
                RaisePropertyChanged(nameof(Volume));
                RaisePropertyChanged(nameof(VolumeText));
                break;
            case nameof(AudioSession.IsMuted):
                RaisePropertyChanged(nameof(IsMuted));
                break;
        }
    }

    private ImageSource? CreateImageSource()
    {
        var icon = AppIconProvider.GetIcon(_session.IconPath, _session.ProcessPath);
        if (icon is null)
        {
            return null;
        }

        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }

    private void RaisePropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
