using HeyComputer.Core.Audio;

namespace HeyComputer.Audio;

public sealed record UtteranceSegmentationOptions(
    TimeSpan PreRoll,
    TimeSpan MinimumSpeech,
    TimeSpan EndSilence,
    TimeSpan MaximumUtterance)
{
    public static UtteranceSegmentationOptions Default { get; } = new(
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(600),
        TimeSpan.FromSeconds(15));

    public void Validate()
    {
        if (PreRoll < TimeSpan.Zero || PreRoll > TimeSpan.FromSeconds(3))
        {
            throw new ArgumentOutOfRangeException(nameof(PreRoll));
        }

        if (MinimumSpeech <= TimeSpan.Zero || EndSilence <= TimeSpan.Zero || MaximumUtterance <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumSpeech), "Speech, silence, and maximum durations must be positive.");
        }

        if (MinimumSpeech > MaximumUtterance || EndSilence > MaximumUtterance)
        {
            throw new ArgumentException("Minimum speech and end silence must not exceed the maximum utterance duration.");
        }
    }
}

public sealed record UtteranceCapture(AudioFrame Audio, bool ReachedMaximumDuration);

public sealed class UtteranceSegmenter
{
    private readonly VoiceActivityDetector _detector;
    private readonly UtteranceSegmentationOptions _options;

    public UtteranceSegmenter(
        VoiceActivityDetector? detector = null,
        UtteranceSegmentationOptions? options = null)
    {
        _detector = detector ?? new VoiceActivityDetector();
        _options = options ?? UtteranceSegmentationOptions.Default;
        _options.Validate();
    }

    public async Task<UtteranceCapture?> CaptureAsync(
        IAsyncEnumerable<AudioFrame> frames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var preRollCapacity = _options.PreRoll == TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(1)
            : _options.PreRoll;
        var preRoll = new RollingAudioBuffer(preRollCapacity);
        var captured = new List<float>();
        var speechStarted = false;
        var voicedSamples = 0;
        var silentSamples = 0;
        var maxSamples = DurationToSamples(_options.MaximumUtterance);
        var minimumSpeechSamples = DurationToSamples(_options.MinimumSpeech);
        var endSilenceSamples = DurationToSamples(_options.EndSilence);

        await foreach (var frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            RollingAudioBuffer.ValidateFrame(frame);
            cancellationToken.ThrowIfCancellationRequested();
            var speech = _detector.IsSpeech(frame);

            if (!speechStarted)
            {
                preRoll.Add(frame);
                if (!speech)
                {
                    continue;
                }

                speechStarted = true;
                var initial = preRoll.Snapshot();
                AppendCapped(captured, initial, maxSamples);
                voicedSamples += frame.Samples.Length;
            }
            else
            {
                AppendCapped(captured, frame.Samples.Span, maxSamples);
                if (speech)
                {
                    voicedSamples += frame.Samples.Length;
                    silentSamples = 0;
                }
                else
                {
                    silentSamples += frame.Samples.Length;
                }
            }

            if (captured.Count >= maxSamples)
            {
                return BuildCapture(captured, reachedMaximumDuration: true);
            }

            if (silentSamples >= endSilenceSamples && voicedSamples >= minimumSpeechSamples)
            {
                return BuildCapture(captured, reachedMaximumDuration: false);
            }
        }

        return speechStarted && voicedSamples >= minimumSpeechSamples
            ? BuildCapture(captured, reachedMaximumDuration: false)
            : null;
    }

    private static int DurationToSamples(TimeSpan duration) =>
        Math.Max(1, checked((int)Math.Ceiling(duration.TotalSeconds * WasapiAudioNormalizer.OutputSampleRate)));

    private static void AppendCapped(List<float> destination, ReadOnlySpan<float> source, int maximum)
    {
        var available = maximum - destination.Count;
        if (available <= 0)
        {
            return;
        }

        var count = Math.Min(available, source.Length);
        for (var index = 0; index < count; index++)
        {
            destination.Add(source[index]);
        }
    }

    private static UtteranceCapture BuildCapture(List<float> samples, bool reachedMaximumDuration) =>
        new(new AudioFrame(samples.ToArray()), reachedMaximumDuration);
}
