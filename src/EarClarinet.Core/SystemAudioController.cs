using System.Runtime.InteropServices;
using EarClarinet.Interop;

namespace EarClarinet.Core;

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
            if (_endpointVolume is not null)
            {
                return;
            }

            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _device = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia);
            _device.Activate(ref EndpointVolumeIid, 0, IntPtr.Zero, out var volumeObject);
            _endpointVolume = (IAudioEndpointVolume)volumeObject;
            _device.Activate(ref MeterInformationIid, 0, IntPtr.Zero, out var meterObject);
            _meterInformation = (IAudioMeterInformation)meterObject;
        }

        Poll();
        _pollTimer = new Timer(_ => Poll(), null, 100, 100);
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
                return;
            }

            try
            {
                _endpointVolume.GetMasterVolumeLevelScalar(out volume);
                isMuted = _endpointVolume.GetMute() != 0;
                peak = _meterInformation!.GetPeakValue();
            }
            catch (Exception)
            {
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
            _endpointVolume.SetMasterVolumeLevelScalar((float)Math.Clamp(linearVolume, 0.0, 1.0), ref guid);
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
            _endpointVolume.SetMute(mute ? 1 : 0, ref guid);
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        lock (_lock)
        {
            if (_endpointVolume is not null)
            {
                Marshal.ReleaseComObject(_endpointVolume);
            }

            if (_meterInformation is not null)
            {
                Marshal.ReleaseComObject(_meterInformation);
            }

            if (_device is not null)
            {
                Marshal.ReleaseComObject(_device);
            }

            if (_enumerator is not null)
            {
                Marshal.ReleaseComObject(_enumerator);
            }

            _endpointVolume = null;
            _meterInformation = null;
            _device = null;
            _enumerator = null;
        }
    }
}
