namespace HeyComputer.WakeWord;

/// <summary>
/// Small seam around a native keyword engine. Implementations own any native stream state.
/// </summary>
public interface IKeywordSpotterRuntime : IDisposable
{
    ValueTask<KeywordDetection?> ProcessAsync(
        ReadOnlyMemory<float> samples,
        int sampleRate,
        CancellationToken cancellationToken = default);

    void Reset();
}

public sealed record KeywordDetection(string Phrase, float? Confidence = null);
