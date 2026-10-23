using HeyComputer.Core.Audio;
using HeyComputer.Core.Personality;

namespace HeyComputer.Core.Providers;

public interface IProvider
{
    string Id { get; }

    ProviderCapabilities Capabilities { get; }
}

public interface IWakeWordDetector : IProvider
{
    Task<bool> WaitForWakeWordAsync(
        IAsyncEnumerable<AudioFrame> audio,
        CancellationToken cancellationToken = default);
}

public interface ITranscriptionProvider : IProvider
{
    Task<TranscriptionResult> TranscribeAsync(
        IAsyncEnumerable<AudioFrame> audio,
        CancellationToken cancellationToken = default);
}

public interface IAgentProvider : IProvider
{
    IAsyncEnumerable<string> StreamResponseAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITextToSpeechProvider : IProvider
{
    Task SpeakAsync(
        IAsyncEnumerable<string> text,
        string? voice,
        CancellationToken cancellationToken = default);
}

public sealed record TranscriptionResult(string Text, string? Language = null, double? Confidence = null)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}

public sealed record ConversationMessage(string Role, string Content);

public sealed record AgentRequest(
    string UserText,
    PersonalityProfile Personality,
    IReadOnlyList<ConversationMessage> History,
    string Instructions);
