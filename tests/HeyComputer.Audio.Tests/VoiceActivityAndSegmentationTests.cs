using System.Runtime.CompilerServices;
using HeyComputer.Core.Audio;

namespace HeyComputer.Audio.Tests;

public sealed class VoiceActivityAndSegmentationTests
{
    [Fact]
    public void Detector_UsesRmsThreshold()
    {
        var detector = new VoiceActivityDetector(new VoiceActivityOptions(0.05f));
        Assert.False(detector.IsSpeech(Frame(160, 0.01f)));
        Assert.True(detector.IsSpeech(Frame(160, 0.1f)));
    }

    [Fact]
    public async Task Segmenter_IncludesPreRollAndStopsAfterSilence()
    {
        var options = new UtteranceSegmentationOptions(
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromSeconds(1));
        var segmenter = new UtteranceSegmenter(new VoiceActivityDetector(new VoiceActivityOptions(0.05f)), options);
        var frames = new[]
        {
            Frame(160, 0.01f),
            Frame(160, 0.1f),
            Frame(160, 0.1f),
            Frame(160, 0.0f),
            Frame(160, 0.0f)
        };

        var result = await segmenter.CaptureAsync(ToAsync(frames));

        Assert.NotNull(result);
        Assert.False(result.ReachedMaximumDuration);
        Assert.Equal(800, result.Audio.Samples.Length);
        Assert.Equal(0.01f, result.Audio.Samples.Span[0]);
    }

    [Fact]
    public async Task Segmenter_EnforcesHardMaximumExactly()
    {
        var options = new UtteranceSegmentationOptions(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(25));
        var segmenter = new UtteranceSegmenter(new VoiceActivityDetector(new VoiceActivityOptions(0.05f)), options);

        var result = await segmenter.CaptureAsync(ToAsync(Enumerable.Repeat(Frame(160, 0.1f), 10)));

        Assert.NotNull(result);
        Assert.True(result.ReachedMaximumDuration);
        Assert.Equal(400, result.Audio.Samples.Length);
    }

    [Fact]
    public async Task Segmenter_ReturnsNullWhenNoSpeechOccurs()
    {
        var segmenter = new UtteranceSegmenter();
        var result = await segmenter.CaptureAsync(ToAsync([Frame(160, 0f), Frame(160, 0.001f)]));
        Assert.Null(result);
    }

    [Fact]
    public async Task Segmenter_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var segmenter = new UtteranceSegmenter();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            segmenter.CaptureAsync(ToAsync([Frame(160, 0.1f)]), cancellation.Token));
    }

    private static AudioFrame Frame(int count, float value) => new(Enumerable.Repeat(value, count).ToArray());

    private static async IAsyncEnumerable<AudioFrame> ToAsync(
        IEnumerable<AudioFrame> frames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return frame;
        }
    }
}
