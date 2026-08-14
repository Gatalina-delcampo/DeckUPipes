using System.Diagnostics;
using System.Runtime.InteropServices;
using EarClarinet.Interop;

namespace EarClarinet.Core;

public sealed class AudioSessionService : IDisposable
{
    // Sessions come and go constantly; a periodic full enumeration keeps the
    // list correct even though the manager's OnSessionCreated callback is not
    // reliably delivered (it never fires for us on some Windows builds).
    private const int ReconcileIntervalMs = 5000;
    private static Guid SessionManager2Iid = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    private readonly object _lock = new();
    private readonly Dictionary<string, SessionRuntime> _byInstanceId = new();
    private readonly List<AudioSession> _displaySessions = new();

    private IMMDeviceEnumerator? _enumerator;
    private IMMDevice? _device;
    private IAudioSessionManager2? _manager;
    private SessionNotificationClient? _notificationClient;
    private Timer? _reconcileTimer;
    private bool _started;

    public event EventHandler? SessionsChanged;

    public IReadOnlyList<AudioSession> Sessions => _displaySessions;

    public void Start()
    {
        lock (_lock)
        {
            if (_started)
            {
                return;
            }

            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _device = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia);
            _device.Activate(ref SessionManager2Iid, 0, IntPtr.Zero, out var managerObject);
            _manager = (IAudioSessionManager2)managerObject;

            _notificationClient = new SessionNotificationClient(this);
            _manager.RegisterSessionNotification(_notificationClient);

            RefreshSessionsLocked();
            _started = true;
        }

        _reconcileTimer = new Timer(_ => Reconcile(), null, ReconcileIntervalMs, ReconcileIntervalMs);
        RaiseSessionsChanged();
    }

    public void SetVolume(AudioSession session, double linearVolume)
    {
        var value = (float)Math.Clamp(linearVolume, 0.0, 1.0);
        var guid = Guid.Empty;
        foreach (var runtime in GetGroupRuntimes(session))
        {
            runtime.SimpleVolume.SetMasterVolume(value, ref guid);
        }
    }

    public void SetMute(AudioSession session, bool mute)
    {
        var guid = Guid.Empty;
        foreach (var runtime in GetGroupRuntimes(session))
        {
            runtime.SimpleVolume.SetMute(mute ? 1 : 0, ref guid);
        }
    }

    public void ToggleMute(AudioSession session) => SetMute(session, !session.IsMuted);

    private void Reconcile()
    {
        lock (_lock)
        {
            if (!_started)
            {
                return;
            }

            RefreshSessionsLocked();
        }

        RaiseSessionsChanged();
    }

    private void RefreshSessionsLocked()
    {
        var enumerator = _manager!.GetSessionEnumerator();
        try
        {
            var count = enumerator.GetCount();
            for (var i = 0; i < count; i++)
            {
                var control = enumerator.GetSession(i);
                TryTrackSession(control);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }

        // State events are not 100% reliable; re-read the state so recency
        // ordering stays accurate even if a callback was missed.
        foreach (var runtime in _byInstanceId.Values)
        {
            try
            {
                runtime.Control.GetState(out var state);
                if (state == AudioSessionState.Active)
                {
                    runtime.Model.MarkActive();
                }
            }
            catch (Exception)
            {
            }
        }

        var staleIds = _byInstanceId.Where(kv => kv.Value.IsExpired).Select(kv => kv.Key).ToArray();
        foreach (var id in staleIds)
        {
            RemoveSessionLocked(id);
        }

        RebuildDisplayListLocked();
    }

    private void TryTrackSession(IAudioSessionControl control)
    {
        IAudioSessionControl2? control2 = control as IAudioSessionControl2;
        if (control2 is null)
        {
            return;
        }

        control2.GetSessionInstanceIdentifier(out var instanceId);
        if (string.IsNullOrEmpty(instanceId) || _byInstanceId.ContainsKey(instanceId))
        {
            return;
        }

        control2.GetState(out var state);
        if (state == AudioSessionState.Expired)
        {
            return;
        }

        uint processId = 0;
        if (control2.GetProcessId(out processId) != 0)
        {
            processId = 0;
        }

        // Never show our own process as a mixer entry.
        if (processId == (uint)Environment.ProcessId)
        {
            return;
        }

        control2.IsSystemSoundsSession(out var isSystemSounds);
        control2.GetDisplayName(out var displayName);
        control2.GetIconPath(out var iconPath);

        ISimpleAudioVolume simpleVolume;
        try
        {
            simpleVolume = (ISimpleAudioVolume)control2;
        }
        catch (Exception)
        {
            return;
        }

        float volume = 0f;
        bool isMuted = false;
        try
        {
            simpleVolume.GetMasterVolume(out volume);
            isMuted = simpleVolume.GetMute() != 0;
        }
        catch (Exception)
        {
        }

        var model = new AudioSession(
            instanceId: instanceId,
            processId: processId,
            processName: GetProcessName(processId),
            displayName: displayName,
            iconPath: iconPath,
            processPath: GetProcessPath(processId),
            isSystemSounds: isSystemSounds,
            volume: volume,
            isMuted: isMuted,
            isActive: state == AudioSessionState.Active);

        if (state == AudioSessionState.Active)
        {
            model.MarkActive();
        }

        var sink = new SessionEventsSink(
            onVolumeChanged: (v, m) => UpdateModel(instanceId, v, m),
            onStateChanged: s => UpdateState(instanceId, s),
            onDisconnected: _ => Expire(instanceId));

        try
        {
            control2.RegisterAudioSessionNotification(sink);
            _byInstanceId[instanceId] = new SessionRuntime(control2, simpleVolume, sink, model);
        }
        catch (Exception)
        {
            return;
        }
    }

    private void RebuildDisplayListLocked()
    {
        _displaySessions.Clear();
        _displaySessions.AddRange(SessionGrouping.SelectDisplaySet(
            _byInstanceId.Values.Select(r => r.Model),
            s => SessionGrouping.GroupKey(s.ProcessId, s.ProcessName, s.InstanceId),
            s => s.IsActive,
            s => s.LastActiveTicks));
    }

    private void UpdateModel(string instanceId, float volume, bool isMuted)
    {
        lock (_lock)
        {
            if (!_byInstanceId.TryGetValue(instanceId, out var runtime))
            {
                return;
            }

            runtime.Model.Volume = volume;
            runtime.Model.IsMuted = isMuted;
        }
    }

    private void UpdateState(string instanceId, AudioSessionState state)
    {
        lock (_lock)
        {
            if (!_byInstanceId.TryGetValue(instanceId, out var runtime))
            {
                return;
            }

            runtime.Model.IsActive = state == AudioSessionState.Active;
            if (state == AudioSessionState.Active)
            {
                runtime.Model.MarkActive();
            }

            if (state == AudioSessionState.Expired)
            {
                runtime.MarkExpired();
            }
        }

        RaiseSessionsChanged();
    }

    private void Expire(string instanceId)
    {
        lock (_lock)
        {
            if (_byInstanceId.TryGetValue(instanceId, out var runtime))
            {
                runtime.MarkExpired();
            }
        }

        Reconcile();
    }

    private void RemoveSessionLocked(string instanceId)
    {
        if (!_byInstanceId.Remove(instanceId, out var runtime))
        {
            return;
        }

        try
        {
            runtime.Control.UnregisterAudioSessionNotification(runtime.Sink);
        }
        catch (Exception)
        {
        }

        Marshal.ReleaseComObject(runtime.Control);
        Marshal.ReleaseComObject(runtime.SimpleVolume);
    }

    internal void HandleSessionCreated(IAudioSessionControl newSession)
    {
        lock (_lock)
        {
            if (!_started)
            {
                return;
            }

            TryTrackSession(newSession);
            RebuildDisplayListLocked();
        }

        RaiseSessionsChanged();
    }

    private void RaiseSessionsChanged() => SessionsChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// All runtimes belonging to the same display entry: with name-based
    /// grouping this can span several processes, so volume/mute hits them all.
    /// </summary>
    private List<SessionRuntime> GetGroupRuntimes(AudioSession session)
    {
        lock (_lock)
        {
            var key = SessionGrouping.GroupKey(session.ProcessId, session.ProcessName, session.InstanceId);
            return _byInstanceId.Values
                .Where(r => SessionGrouping.GroupKey(r.Model.ProcessId, r.Model.ProcessName, r.Model.InstanceId) == key)
                .ToList();
        }
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return $"pid {processId}";
        }
    }

    private static string GetProcessPath(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _reconcileTimer?.Dispose();
        _reconcileTimer = null;

        lock (_lock)
        {
            _started = false;

            if (_manager is not null && _notificationClient is not null)
            {
                try
                {
                    _manager.UnregisterSessionNotification(_notificationClient);
                }
                catch (Exception)
                {
                }
            }

            foreach (var id in _byInstanceId.Keys.ToArray())
            {
                RemoveSessionLocked(id);
            }

            _byInstanceId.Clear();
            _displaySessions.Clear();

            if (_manager is not null)
            {
                Marshal.ReleaseComObject(_manager);
            }

            if (_device is not null)
            {
                Marshal.ReleaseComObject(_device);
            }

            if (_enumerator is not null)
            {
                Marshal.ReleaseComObject(_enumerator);
            }

            _manager = null;
            _device = null;
            _enumerator = null;
            _notificationClient = null;
        }
    }

    private sealed class SessionRuntime
    {
        public IAudioSessionControl2 Control { get; }
        public ISimpleAudioVolume SimpleVolume { get; }
        public SessionEventsSink Sink { get; }
        public AudioSession Model { get; }
        public bool IsExpired { get; private set; }

        public SessionRuntime(IAudioSessionControl2 control, ISimpleAudioVolume simpleVolume, SessionEventsSink sink, AudioSession model)
        {
            Control = control;
            SimpleVolume = simpleVolume;
            Sink = sink;
            Model = model;
        }

        public void MarkExpired() => IsExpired = true;
    }
}
