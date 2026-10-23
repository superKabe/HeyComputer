using HeyComputer.Core.Audio;

namespace HeyComputer.Core.Tests;

public sealed class AudioFrameTests
{
    [Fact]
    public void Constructor_RejectsInvalidFormatAndEmptyFrames()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrame(new float[1], sampleRate: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrame(new float[1], channels: 0));
        Assert.Throws<ArgumentException>(() => new AudioFrame(ReadOnlyMemory<float>.Empty));
    }

    [Fact]
    public void Constructor_RejectsOversizedFrames()
    {
        Assert.Throws<ArgumentException>(() => new AudioFrame(new float[160_001], sampleRate: 16_000));
    }

    [Fact]
    public void Duration_UsesSampleRateAndChannelCount()
    {
        var frame = new AudioFrame(new float[32_000], sampleRate: 16_000, channels: 2);

        Assert.Equal(TimeSpan.FromSeconds(1), frame.Duration);
    }
}
