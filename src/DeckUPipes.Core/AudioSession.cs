using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeckUPipes.Core;

public sealed class AudioSession : INotifyPropertyChanged
{
    private double _volume;
    private bool _isMuted;
    private bool _isActive;

    public string InstanceId { get; }
    public uint ProcessId { get; }
    public string ProcessName { get; }
    public string DisplayName { get; }
    public string IconPath { get; }
    public string ProcessPath { get; }
    public bool IsSystemSounds { get; }

    public double Volume
    {
        get => _volume;
        set
        {
            if (Set(ref _volume, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (Set(ref _isMuted, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMuted)));
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (Set(ref _isActive, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }
    }

    /// <summary>
    /// TickCount64 of the last time this session produced audio. Drives the
    /// display order (most recent first). 0 = never active.
    /// </summary>
    public long LastActiveTicks { get; private set; }

    internal void MarkActive() => LastActiveTicks = Environment.TickCount64;

    public AudioSession(
        string instanceId,
        uint processId,
        string processName,
        string displayName,
        string iconPath,
        string processPath,
        bool isSystemSounds,
        double volume,
        bool isMuted,
        bool isActive)
    {
        InstanceId = instanceId;
        ProcessId = processId;
        ProcessName = processName;
        DisplayName = ResolveDisplayName(displayName, processName, isSystemSounds, processId);
        IconPath = iconPath;
        ProcessPath = processPath;
        IsSystemSounds = isSystemSounds;
        _volume = volume;
        _isMuted = isMuted;
        _isActive = isActive;
    }

    public static string ResolveDisplayName(string raw, string processName, bool isSystemSounds, uint processId)
    {
        if (isSystemSounds || (processId == 0 && raw.StartsWith("@%", StringComparison.Ordinal)))
        {
            return "System Sounds";
        }

        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("@%", StringComparison.Ordinal))
        {
            return processName;
        }

        return raw;
    }

    public override string ToString() => $"{DisplayName} ({ProcessName}, pid={ProcessId})";

    private bool Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

