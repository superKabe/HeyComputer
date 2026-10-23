using HeyComputer.Core.Audio;

namespace HeyComputer.WakeWord.Tests;

public sealed class StreamingWakeWordDetectorTests
{
    [Fact]
    public async Task ReturnsTrueWhenDefaultPhraseIsDetected()
    {
        using var runtime = new ScriptedRuntime([null, new(StreamingWakeWordDetector.DefaultPhrase)]);
        using var detector = new StreamingWakeWordDetector(runtime);

        var detected = await detector.WaitForWakeWordAsync(Frames(2));

        Assert.True(detected);
        Assert.Equal(2, runtime.FramesProcessed);
        Assert.Equal(2, runtime.ResetCount);
    }

    [Fact]
    public async Task IgnoresDifferentPhrase()
    {
        using var runtime = new ScriptedRuntime([new("Hello Assistant")]);
        using var detector = new StreamingWakeWordDetector(runtime);

        var detected = await detector.WaitForWakeWordAsync(Frames(1));

        Assert.False(detected);
    }

    [Fact]
    public async Task RejectsAudioThatWasNotNormalized()
    {
        using var runtime = new ScriptedRuntime([]);
        using var detector = new StreamingWakeWordDetector(runtime);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await detector.WaitForWakeWordAsync(Frames(1, sampleRate: 48_000)));
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        using var runtime = new BlockingRuntime();
        using var detector = new StreamingWakeWordDetector(runtime);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await detector.WaitForWakeWordAsync(EndlessFrames(cancellation.Token), cancellation.Token));
    }

    private static async IAsyncEnumerable<AudioFrame> Frames(int count, int sampleRate = 16_000)
    {
        for (var index = 0; index < count; index++)
        {
            yield return new AudioFrame(new float[160], sampleRate, 1);
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<AudioFrame> EndlessFrames(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AudioFrame(new float[160]);
            await Task.Delay(10, cancellationToken);
        }
    }

    private sealed class ScriptedRuntime(IEnumerable<KeywordDetection?> detections) : IKeywordSpotterRuntime
    {
        private readonly Queue<KeywordDetection?> _detections = new(detections);

        public int FramesProcessed { get; private set; }

        public int ResetCount { get; private set; }

        public ValueTask<KeywordDetection?> ProcessAsync(
            ReadOnlyMemory<float> samples,
            int sampleRate,
            CancellationToken cancellationToken = default)
        {
            FramesProcessed++;
            return ValueTask.FromResult(_detections.Count > 0 ? _detections.Dequeue() : null);
        }

        public void Reset() => ResetCount++;

        public void Dispose()
        {
        }
    }

    private sealed class BlockingRuntime : IKeywordSpotterRuntime
    {
        public async ValueTask<KeywordDetection?> ProcessAsync(
            ReadOnlyMemory<float> samples,
            int sampleRate,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }
}
