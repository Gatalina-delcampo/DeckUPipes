using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeckUPipes.App.Services;
using DeckUPipes.Core;

namespace DeckUPipes.App.UI;

/// <summary>
/// UI wrapper over an AudioSession. Keeps the volume/mute data flowing from
/// the audio service into the overlay via INotifyPropertyChanged.
/// </summary>
public sealed class SessionViewModel : INotifyPropertyChanged
{
    private const int PixelArtThreshold = 64;

    private readonly AudioSession _session;
    private bool _isFocused;
    private bool _isNextTabTarget;
    private bool _shortcutHintsEnabled;

    public SessionViewModel(AudioSession session)
    {
        _session = session;
        _session.PropertyChanged += OnSessionPropertyChanged;
        Icon = AppIconProvider.GetIcon(_session.IconPath, _session.ProcessPath);
    }

    public AudioSession Session => _session;

    public string InstanceId => _session.InstanceId;

    public string DisplayName => _session.DisplayName;

    public BitmapSource? Icon { get; }

    public ImageSource? SkinIcon { get; private set; }

    public ImageSource? SkinVolumeSegmentImage { get; private set; }

    public ImageSource? SkinMuteImage { get; private set; }

    public BitmapScalingMode IconScalingMode =>
        Icon is { PixelWidth: > 0 and <= PixelArtThreshold }
            ? BitmapScalingMode.NearestNeighbor
            : BitmapScalingMode.HighQuality;

    public IReadOnlyList<VolumeSegmentState> VolumeSegments => Enumerable.Range(0, 10)
        .Select(index => new VolumeSegmentState(
            index < Math.Ceiling(_session.Volume * 10) ? (IsMuted ? 0.45 : 1.0) : 0.0,
            SkinVolumeSegmentImage))
        .ToList();

    public double IconOpacity => IsMuted ? 0.45 : 1.0;

    public double MuteOverlayOpacity => IsMuted ? 0.65 : 0.0;

    public void ApplySkinAssets(ImageSource? skinIcon, ImageSource? volumeSegmentImage, ImageSource? muteImage)
    {
        SkinIcon = skinIcon;
        SkinVolumeSegmentImage = volumeSegmentImage;
        SkinMuteImage = muteImage;
        RaisePropertyChanged(nameof(SkinIcon));
        RaisePropertyChanged(nameof(SkinVolumeSegmentImage));
        RaisePropertyChanged(nameof(SkinMuteImage));
        RaisePropertyChanged(nameof(VolumeSegments));
    }

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

    /// <summary>True when this row is the next Tab target. Driven by the classic overlay.</summary>
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

    /// <summary>Mirror of the shortcut-hint switch, pushed in by the classic overlay.</summary>
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

    /// <summary>True when the focused row should show its mute hint chip.</summary>
    public bool IsMuteHint => _shortcutHintsEnabled && _isFocused;

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AudioSession.Volume):
                RaisePropertyChanged(nameof(Volume));
                RaisePropertyChanged(nameof(VolumeText));
                RaisePropertyChanged(nameof(VolumeSegments));
                break;
            case nameof(AudioSession.IsMuted):
                RaisePropertyChanged(nameof(IsMuted));
                RaisePropertyChanged(nameof(VolumeSegments));
                RaisePropertyChanged(nameof(IconOpacity));
                RaisePropertyChanged(nameof(MuteOverlayOpacity));
                break;
        }
    }

    private void RaisePropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public event PropertyChangedEventHandler? PropertyChanged;
}
