using System.Runtime.CompilerServices;
using HeyComputer.Core.Audio;
using HeyComputer.Core.Providers;

namespace HeyComputer.Core.Testing;

public sealed class FakeWakeWordDetector(bool detected = true) : IWakeWordDetector
{
    public string Id => "fake-wake-word";

    public ProviderCapabilities Capabilities { get; } = new(true, false, true, [16_000]);

    public async Task<bool> WaitForWakeWordAsync(
        IAsyncEnumerable<AudioFrame> audio,
        CancellationToken cancellationToken = default)
    {
        await foreach (var _ in audio.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            return detected;
        }

        return false;
    }
}

public sealed class FakeTranscriptionProvider(string transcript = "test request") : ITranscriptionProvider
{
    public string Id => "fake-transcription";

    public ProviderCapabilities Capabilities { get; } = new(true, true, true, [16_000]);

    public int FramesReceived { get; private set; }

    public async Task<TranscriptionResult> TranscribeAsync(
        IAsyncEnumerable<AudioFrame> audio,
        CancellationToken cancellationToken = default)
    {
        await foreach (var _ in audio.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            FramesReceived++;
        }

        return new TranscriptionResult(transcript, "en", 1);
    }
}

public sealed class FakeAgentProvider(params string[] chunks) : IAgentProvider
{
    private readonly string[] _chunks = chunks.Length == 0 ? ["test response"] : chunks;

    public string Id => "fake-agent";

    public ProviderCapabilities Capabilities { get; } = new(true, true, true);

    public AgentRequest? LastRequest { get; private set; }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        foreach (var chunk in _chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return chunk;
        }
    }
}

public sealed class FakeTextToSpeechProvider : ITextToSpeechProvider
{
    public string Id => "fake-speech";

    public ProviderCapabilities Capabilities { get; } = new(true, true, true, Voices: ["fake"]);

    public string SpokenText { get; private set; } = string.Empty;

    public string? SelectedVoice { get; private set; }

    public async Task SpeakAsync(
        IAsyncEnumerable<string> text,
        string? voice,
        CancellationToken cancellationToken = default)
    {
        SelectedVoice = voice;
        await foreach (var chunk in text.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            SpokenText += chunk;
        }
    }
}
