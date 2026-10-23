using HeyComputer.Core.Audio;

namespace HeyComputer.Audio;

public sealed class RollingAudioBuffer
{
    private readonly object _gate = new();
    private readonly int _capacitySamples;
    private readonly Queue<float> _samples;

    public RollingAudioBuffer(TimeSpan capacity)
    {
        if (capacity <= TimeSpan.Zero || capacity > TimeSpan.FromSeconds(10))
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero and no more than ten seconds.");
        }

        _capacitySamples = Math.Max(1, (int)Math.Ceiling(capacity.TotalSeconds * WasapiAudioNormalizer.OutputSampleRate));
        _samples = new Queue<float>(_capacitySamples);
    }

    public void Add(AudioFrame frame)
    {
        ValidateFrame(frame);
        lock (_gate)
        {
            foreach (var sample in frame.Samples.Span)
            {
                if (_samples.Count == _capacitySamples)
                {
                    _samples.Dequeue();
                }

                _samples.Enqueue(sample);
            }
        }
    }

    public float[] Snapshot()
    {
        lock (_gate)
        {
            return _samples.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _samples.Clear();
        }
    }

    internal static void ValidateFrame(AudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.SampleRate != WasapiAudioNormalizer.OutputSampleRate || frame.Channels != 1)
        {
            throw new ArgumentException("Audio must be normalized to 16 kHz mono.", nameof(frame));
        }
    }
}
