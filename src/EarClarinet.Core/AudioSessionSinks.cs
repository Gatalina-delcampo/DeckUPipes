using EarClarinet.Interop;

namespace EarClarinet.Core;

internal sealed class SessionEventsSink : IAudioSessionEvents
{
    private readonly Action<float, bool> _onVolumeChanged;
    private readonly Action<AudioSessionState> _onStateChanged;
    private readonly Action<AudioSessionDisconnectReason> _onDisconnected;

    public SessionEventsSink(
        Action<float, bool> onVolumeChanged,
        Action<AudioSessionState> onStateChanged,
        Action<AudioSessionDisconnectReason> onDisconnected)
    {
        _onVolumeChanged = onVolumeChanged;
        _onStateChanged = onStateChanged;
        _onDisconnected = onDisconnected;
    }

    public void OnDisplayNameChanged(string newDisplayName, ref Guid eventContext)
    {
    }

    public void OnIconPathChanged(string newIconPath, ref Guid eventContext)
    {
    }

    public void OnSimpleVolumeChanged(float newVolume, int newMute, ref Guid eventContext) =>
        _onVolumeChanged(newVolume, newMute != 0);

    public void OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel, ref Guid eventContext)
    {
    }

    public void OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext)
    {
    }

    public void OnStateChanged(AudioSessionState newState) => _onStateChanged(newState);

    public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason) =>
        _onDisconnected(disconnectReason);
}

internal sealed class SessionNotificationClient : IAudioSessionNotification
{
    private readonly AudioSessionService _owner;

    public SessionNotificationClient(AudioSessionService owner) => _owner = owner;

    public void OnSessionCreated(IAudioSessionControl newSession) => _owner.HandleSessionCreated(newSession);
}
