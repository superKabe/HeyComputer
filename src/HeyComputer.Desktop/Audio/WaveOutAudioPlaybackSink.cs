using HeyComputer.Providers.Xai.Speech;
using NAudio.Wave;

namespace HeyComputer.Desktop.Audio;

public sealed class WaveOutAudioPlaybackSink : IAudioPlaybackSink, IDisposable
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private BufferedWaveProvider? _buffer;
    private StreamingAudioFormat? _format;
    private bool _disposed;

    public ValueTask PlayAsync(
        ReadOnlyMemory<byte> audio,
        StreamingAudioFormat format,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (audio.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureOutput(format);
            var bytes = audio.ToArray();
            _buffer!.AddSamples(bytes, 0, bytes.Length);
            if (_output!.PlaybackState != PlaybackState.Playing)
            {
                _output.Play();
            }
        }

        return ValueTask.CompletedTask;
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _output?.Stop();
            _buffer?.ClearBuffer();
        }
    }

    private void EnsureOutput(StreamingAudioFormat format)
    {
        if (!string.Equals(format.Codec, "pcm", StringComparison.OrdinalIgnoreCase) ||
            format.BitsPerSample != 16 || format.SampleRate <= 0 || format.Channels <= 0)
        {
            throw new NotSupportedException("The desktop currently supports PCM16 speech playback only.");
        }

        if (_format == format)
        {
            return;
        }

        _output?.Dispose();
        _buffer = new BufferedWaveProvider(
            new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels))
        {
            BufferDuration = TimeSpan.FromSeconds(10),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        _output = new WaveOutEvent();
        _output.Init(_buffer);
        _format = format;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _buffer = null;
        }

        GC.SuppressFinalize(this);
    }
}
