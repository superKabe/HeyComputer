using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace HeyComputer.Desktop.Audio;

public sealed class AudioCaptureService : IAudioCaptureService, IMMNotificationClient
{
    private readonly object _gate = new();
    private readonly MMDeviceEnumerator _enumerator = new();
    private WasapiCapture? _capture;
    private bool _disposed;

    public AudioCaptureService()
    {
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public event EventHandler<AudioFrameEventArgs>? FrameAvailable;
    public event EventHandler<float>? LevelChanged;
    public event EventHandler<string>? CaptureFailed;
    public event EventHandler? DevicesChanged;

    public bool IsCapturing { get; private set; }
    public string? SelectedDeviceId { get; set; }

    public IReadOnlyList<MicrophoneDevice> GetDevices()
    {
        ThrowIfDisposed();
        var devices = new List<MicrophoneDevice>
        {
            new(null, "System default (communications)")
        };

        devices.AddRange(
            _enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(device => new MicrophoneDevice(device.ID, device.FriendlyName))
                .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase));

        return devices;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        lock (_gate)
        {
            if (IsCapturing)
            {
                return Task.CompletedTask;
            }

            try
            {
                var device = ResolveDevice();
                _capture = new WasapiCapture(device);
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
                IsCapturing = true;
            }
            catch (Exception exception)
            {
                ReleaseCapture();
                CaptureFailed?.Invoke(this, SanitizeError(exception));
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_capture is null)
            {
                IsCapturing = false;
                return Task.CompletedTask;
            }

            try
            {
                _capture.StopRecording();
            }
            finally
            {
                ReleaseCapture();
            }
        }

        return Task.CompletedTask;
    }

    private MMDevice ResolveDevice()
    {
        if (string.IsNullOrWhiteSpace(SelectedDeviceId))
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }

        return _enumerator.GetDevice(SelectedDeviceId);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _capture;
        if (capture is null || e.BytesRecorded == 0)
        {
            return;
        }

        var copiedBuffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, copiedBuffer, 0, e.BytesRecorded);
        LevelChanged?.Invoke(this, CalculatePeak(copiedBuffer, capture.WaveFormat));
        FrameAvailable?.Invoke(this, new AudioFrameEventArgs(copiedBuffer, capture.WaveFormat));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsCapturing = false;
        if (e.Exception is not null)
        {
            CaptureFailed?.Invoke(this, SanitizeError(e.Exception));
        }
    }

    private static float CalculatePeak(byte[] buffer, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            var peak = 0f;
            for (var offset = 0; offset + 4 <= buffer.Length; offset += 4)
            {
                peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(buffer, offset)));
            }

            return Math.Clamp(peak, 0f, 1f);
        }

        if (format.BitsPerSample == 16)
        {
            var peak = 0f;
            for (var offset = 0; offset + 2 <= buffer.Length; offset += 2)
            {
                peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(buffer, offset) / 32768f));
            }

            return Math.Clamp(peak, 0f, 1f);
        }

        return 0f;
    }

    private static string SanitizeError(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Windows denied microphone access.",
        _ => "The selected microphone stopped working."
    };

    private void ReleaseCapture()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        IsCapturing = false;
    }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => NotifyDevicesChanged();
    public void OnDeviceAdded(string pwstrDeviceId) => NotifyDevicesChanged();
    public void OnDeviceRemoved(string deviceId) => NotifyDevicesChanged();

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow is DataFlow.Capture or DataFlow.All)
        {
            NotifyDevicesChanged();
        }
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) => NotifyDevicesChanged();

    private void NotifyDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync();
        _enumerator.UnregisterEndpointNotificationCallback(this);
        _enumerator.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
