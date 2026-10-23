namespace HeyComputer.Desktop.Audio;

public interface IAudioCaptureService : IAsyncDisposable
{
    event EventHandler<AudioFrameEventArgs>? FrameAvailable;
    event EventHandler<float>? LevelChanged;
    event EventHandler<string>? CaptureFailed;
    event EventHandler? DevicesChanged;

    bool IsCapturing { get; }
    string? SelectedDeviceId { get; set; }

    IReadOnlyList<MicrophoneDevice> GetDevices();
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
