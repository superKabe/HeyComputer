using HeyComputer.Core.Audio;

namespace HeyComputer.Audio.Tests;

public sealed class BufferAndChannelTests
{
    [Fact]
    public void RollingBuffer_KeepsOnlyNewestSamples()
    {
        var buffer = new RollingAudioBuffer(TimeSpan.FromMilliseconds(1));
        buffer.Add(Frame(Enumerable.Range(0, 32).Select(value => (float)value).ToArray()));

        Assert.Equal(16, buffer.Snapshot().Length);
        Assert.Equal(16f, buffer.Snapshot()[0]);
        buffer.Clear();
        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public async Task BoundedChannel_DropsOldestWithoutBlocking()
    {
        var channel = new BoundedAudioFrameChannel(2);
        Assert.True(channel.TryWrite(Frame([1])));
        Assert.True(channel.TryWrite(Frame([2])));
        Assert.True(channel.TryWrite(Frame([3])));
        channel.TryComplete();

        var values = new List<float>();
        await foreach (var frame in channel.ReadAllAsync())
        {
            values.Add(frame.Samples.Span[0]);
        }

        Assert.Equal([2f, 3f], values);
    }

    [Fact]
    public void RollingBuffer_RejectsNonNormalizedFrames()
    {
        var buffer = new RollingAudioBuffer(TimeSpan.FromMilliseconds(100));
        Assert.Throws<ArgumentException>(() => buffer.Add(new AudioFrame(new float[10], 48_000, 1)));
    }

    private static AudioFrame Frame(float[] samples) => new(samples);
}
