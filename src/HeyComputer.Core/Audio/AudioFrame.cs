namespace HeyComputer.Core.Audio;

public sealed record AudioFrame
{
    public AudioFrame(ReadOnlyMemory<float> samples, int sampleRate = 16_000, int channels = 1)
        : this(samples, sampleRate, channels, DateTimeOffset.UtcNow)
    {
    }

    public AudioFrame(ReadOnlyMemory<float> samples, int sampleRate, int channels, DateTimeOffset capturedAt)
    {
        if (sampleRate is < 8_000 or > 384_000)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be between 8 kHz and 384 kHz.");
        }

        if (channels is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), "Channel count must be between 1 and 32.");
        }

        if (samples.IsEmpty)
        {
            throw new ArgumentException("An audio frame must contain at least one sample.", nameof(samples));
        }

        var maximumSamples = checked(sampleRate * channels * 10);
        if (samples.Length > maximumSamples)
        {
            throw new ArgumentException("An audio frame cannot contain more than ten seconds of audio.", nameof(samples));
        }

        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
        CapturedAt = capturedAt;
    }

    public ReadOnlyMemory<float> Samples { get; }

    public int SampleRate { get; }

    public int Channels { get; }

    public DateTimeOffset CapturedAt { get; }

    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / (SampleRate * Channels));
}
