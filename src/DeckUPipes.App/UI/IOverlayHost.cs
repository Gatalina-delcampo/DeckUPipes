using DeckUPipes.Core;

namespace DeckUPipes.App.UI;

/// <summary>
/// What the app needs from an overlay, so the tray icon, the global hotkey and the
/// settings window can drive either the modern overlay or the classic one without
/// caring which is on screen.
/// </summary>
public interface IOverlayHost
{
    void Toggle();

    void RefreshSessions(IReadOnlyList<AudioSession> sessions);

    void UpdateSystemVolume(double volume, bool isMuted, float peak);

    void Reposition();

    void ApplySettings();
}
