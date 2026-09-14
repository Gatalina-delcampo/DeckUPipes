using System.Runtime.InteropServices;
using DeckUPipes.Interop;

namespace DeckUPipes.Core;

public sealed class SystemAudioController : IDisposable
{
    private static Guid EndpointVolumeIid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static Guid MeterInformationIid = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");

    private readonly object _lock = new();
    private IMMDeviceEnumerator? _enumerator;
    private IMMDevice? _device;
    private IAudioEndpointVolume? _endpointVolume;
    private IAudioMeterInformation? _meterInformation;
    private Timer? _pollTimer;

    public event EventHandler? StateChanged;

    public double Volume { get; private set; }
    public bool IsMuted { get; private set; }
    public float Peak { get; private set; }

    public void Start()
    {
        lock (_lock)
        {
            if (_endpointVolume is null)
            {
                AttachEndpointLocked();
            }
        }

        Poll();
        _pollTimer = new Timer(_ => Poll(), null, 100, 100);
    }

    /// <summary>Binds to whatever endpoint Windows currently treats as the default.</summary>
    private void AttachEndpointLocked()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _device = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia);
        _device.Activate(ref EndpointVolumeIid, 0, IntPtr.Zero, out var volumeObject);
        _endpointVolume = (IAudioEndpointVolume)volumeObject;
        _device.Activate(ref MeterInformationIid, 0, IntPtr.Zero, out var meterObject);
        _meterInformation = (IAudioMeterInformation)meterObject;
    }

    private void DetachEndpointLocked()
    {
        if (_endpointVolume is not null)
        {
            Marshal.ReleaseComObject(_endpointVolume);
            _endpointVolume = null;
        }

        if (_meterInformation is not null)
        {
            Marshal.ReleaseComObject(_meterInformation);
            _meterInformation = null;
        }

        if (_device is not null)
        {
            Marshal.ReleaseComObject(_device);
            _device = null;
        }

        if (_enumerator is not null)
        {
            Marshal.ReleaseComObject(_enumerator);
            _enumerator = null;
        }
    }

    private void Poll()
    {
        float volume = 0f;
        bool isMuted = false;
        float peak = 0f;

        lock (_lock)
        {
            if (_endpointVolume is null)
            {
                // The previous endpoint was removed; pick up the current default.
                try
                {
                    AttachEndpointLocked();
                }
                catch (Exception)
                {
                    return;
                }
            }

            try
            {
                _endpointVolume!.GetMasterVolumeLevelScalar(out volume);
                isMuted = _endpointVolume.GetMute() != 0;
                peak = _meterInformation!.GetPeakValue();
            }
            catch (Exception)
            {
                // The endpoint went away mid-read: drop it and re-attach on the next tick.
                DetachEndpointLocked();
                return;
            }
        }

        var changed = Math.Abs(Volume - volume) > 0.0001 || IsMuted != isMuted || Math.Abs(Peak - peak) > 0.0001;
        Volume = volume;
        IsMuted = isMuted;
        Peak = peak;

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetVolume(double linearVolume)
    {
        lock (_lock)
        {
            if (_endpointVolume is null)
            {
                return;
            }

            var guid = Guid.Empty;
            try
            {
                _endpointVolume.SetMasterVolumeLevelScalar((float)Math.Clamp(linearVolume, 0.0, 1.0), ref guid);
            }
            catch (Exception)
            {
                // The endpoint disappeared mid-adjustment; the poll re-attaches.
                DetachEndpointLocked();
            }
        }
    }

    public void SetMute(bool mute)
    {
        lock (_lock)
        {
            if (_endpointVolume is null)
            {
                return;
            }

            var guid = Guid.Empty;
            try
            {
                _endpointVolume.SetMute(mute ? 1 : 0, ref guid);
            }
            catch (Exception)
            {
                DetachEndpointLocked();
            }
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        lock (_lock)
        {
            DetachEndpointLocked();
        }
    }
}

