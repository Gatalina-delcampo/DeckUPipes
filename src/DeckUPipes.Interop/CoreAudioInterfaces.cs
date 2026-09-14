using System.Runtime.InteropServices;

namespace DeckUPipes.Interop;

// Windows Core Audio (mmdeviceapi/audioclient) COM bindings, following the
// EarTrumpet conventions that keep them working on modern .NET:
//  - Interfaces are declared WITHOUT [ComImport] (only [Guid] + [InterfaceType]),
//    so managed sink objects (IAudioSessionEvents/IAudioSessionNotification)
//    marshal to COM callbacks through the CCW correctly.
//  - Vtables are FLAT (no interface inheritance) to avoid slot misalignment.
//  - Strings are marshaled as return values, BOOLs as int, HRESULTs via
//    [PreserveSig] where a failure is not exceptional.
//  - IAudioSessionControl2 uses the CURRENT IID (BFB7FF88-7239-4FC9-8FA2-...);
//    the older SDK IID (2799-4FC9-81FE-...) is not implemented on Windows 11.
//  - Per-session volume/mute comes from QI'ing the session object itself for
//    ISimpleAudioVolume (identity guaranteed), not from GetSimpleAudioVolume.

public enum EDataFlow
{
    Render = 0,
    Capture = 1,
    All = 2,
}

public enum ERole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2,
}

public enum AudioSessionState
{
    Inactive = 0,
    Active = 1,
    Expired = 2,
}

public enum AudioSessionDisconnectReason
{
    DeviceRemoval = 0,
    ServerShutdown = 1,
    FormatChanged = 2,
    SessionLogoff = 3,
    SessionDisconnected = 4,
    ExclusiveModeOverride = 5,
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
public class MMDeviceEnumeratorComObject
{
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceEnumerator
{
    [return: MarshalAs(UnmanagedType.Interface)]
    IMMDeviceCollection EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask);
    [return: MarshalAs(UnmanagedType.Interface)]
    IMMDevice GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role);
    [return: MarshalAs(UnmanagedType.Interface)]
    IMMDevice GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId);
    void RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] object pClient);
    void UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] object pClient);
}

[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDeviceCollection
{
    uint GetCount();
    [return: MarshalAs(UnmanagedType.Interface)]
    IMMDevice Item(uint nDevice);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMDevice
{
    // Declaration order IS the vtable order. Windows defines these as Activate,
    // OpenPropertyStore, GetId, GetState - swapping any pair silently calls the
    // wrong function instead of failing to compile.
    void Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    void OpenPropertyStore(uint stgmAccess, out IntPtr ppProperties);
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetId();
    uint GetState();
}

[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionManager2
{
    void GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl sessionControl);
    void GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, [MarshalAs(UnmanagedType.Interface)] out ISimpleAudioVolume simpleVolume);
    [return: MarshalAs(UnmanagedType.Interface)]
    IAudioSessionEnumerator GetSessionEnumerator();
    void RegisterSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionNotification sessionNotification);
    void UnregisterSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionNotification sessionNotification);
    void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, [MarshalAs(UnmanagedType.Interface)] object duckNotification);
    void UnregisterDuckNotification([MarshalAs(UnmanagedType.Interface)] object duckNotification);
}

[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionEnumerator
{
    int GetCount();
    [return: MarshalAs(UnmanagedType.Interface)]
    IAudioSessionControl GetSession(int index);
}

[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionControl
{
    AudioSessionState GetState();
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetDisplayName();
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetIconPath();
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
    Guid GetGroupingParam();
    void SetGroupingParam(ref Guid groupingParamOverride, ref Guid eventContext);
    void RegisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents newNotifications);
    void UnregisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents oldNotifications);
}

[Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionControl2
{
    void GetState(out AudioSessionState state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
    void GetGroupingParam(out Guid groupingParam);
    void SetGroupingParam(ref Guid groupingParam, ref Guid eventContext);
    void RegisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents newNotifications);
    void UnregisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents oldNotifications);
    void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
    void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);
    [PreserveSig]
    int GetProcessId(out uint processId);
    void IsSystemSoundsSession(out bool isSystemSoundsSession);
    void SetDuckingPreference(int optOut);
}

[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISimpleAudioVolume
{
    void SetMasterVolume(float levelNorm, ref Guid eventContext);
    void GetMasterVolume(out float levelNorm);
    void SetMute(int isMuted, ref Guid eventContext);
    int GetMute();
}

[Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionEvents
{
    void OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string newDisplayName, ref Guid eventContext);
    void OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string newIconPath, ref Guid eventContext);
    void OnSimpleVolumeChanged(float newVolume, int newMute, ref Guid eventContext);
    void OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel, ref Guid eventContext);
    void OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext);
    void OnStateChanged(AudioSessionState newState);
    void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason);
}

[Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionNotification
{
    void OnSessionCreated([MarshalAs(UnmanagedType.Interface)] IAudioSessionControl newSession);
}

[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioMeterInformation
{
    float GetPeakValue();
    uint GetMeteringChannelCount();
    [PreserveSig]
    int GetChannelsPeakValues(uint channelCount, IntPtr peakValues);
    void QueryHardwareSupport(out uint hardwareSupportMask);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] object notify);
    void UnregisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] object notify);
    uint GetChannelCount();
    void SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
    void SetMasterVolumeLevelScalar(float levelNorm, ref Guid eventContext);
    void GetMasterVolumeLevel(out float levelDb);
    void GetMasterVolumeLevelScalar(out float levelNorm);
    void SetChannelVolumeLevel(uint channelNumber, float levelDb, ref Guid eventContext);
    void SetChannelVolumeLevelScalar(uint channelNumber, float levelNorm, ref Guid eventContext);
    void GetChannelVolumeLevel(uint channelNumber, out float levelDb);
    float GetChannelVolumeLevelScalar(uint channelNumber);
    void SetMute(int isMuted, ref Guid eventContext);
    int GetMute();
    void GetVolumeStepInfo(out uint step, out uint stepCount);
    void VolumeStepUp(ref Guid eventContext);
    void VolumeStepDown(ref Guid eventContext);
    void QueryHardwareSupport(out uint hardwareSupportMask);
    void GetVolumeRange(out float minLevelDb, out float maxLevelDb, out float incrementDb);
}

