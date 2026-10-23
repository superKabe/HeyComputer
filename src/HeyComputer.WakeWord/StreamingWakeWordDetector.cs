using HeyComputer.Core.Audio;
using HeyComputer.Core.Providers;

namespace HeyComputer.WakeWord;

public sealed class StreamingWakeWordDetector : IWakeWordDetector, IDisposable
{
    public const string DefaultPhrase = "Hey Computer";

    private readonly IKeywordSpotterRuntime _runtime;
    private readonly string _phrase;
    private bool _disposed;

    public StreamingWakeWordDetector(IKeywordSpotterRuntime runtime, string phrase = DefaultPhrase)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(phrase);
        _runtime = runtime;
        _phrase = phrase.Trim();
    }

    public string Id => "local-wake-word";

    public ProviderCapabilities Capabilities { get; } = new(
        IsLocal: true,
        SupportsStreaming: true,
        SupportsCancellation: true,
        SupportedSampleRates: [16_000]);

    public async Task<bool> WaitForWakeWordAsync(
        IAsyncEnumerable<AudioFrame> audio,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _runtime.Reset();

        await foreach (var frame in audio.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ValidateFrame(frame);
            var result = await _runtime
                .ProcessAsync(frame.Samples, frame.SampleRate, cancellationToken)
                .ConfigureAwait(false);

            if (result is not null &&
                string.Equals(result.Phrase.Trim(), _phrase, StringComparison.OrdinalIgnoreCase))
            {
                _runtime.Reset();
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _runtime.Dispose();
    }

    private static void ValidateFrame(AudioFrame frame)
    {
        if (frame.SampleRate != 16_000 || frame.Channels != 1)
        {
            throw new ArgumentException(
                "Wake-word audio must be normalized to 16 kHz mono floating-point samples.",
                nameof(frame));
        }
    }
}
