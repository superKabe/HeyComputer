using HeyComputer.Core.Providers;

namespace HeyComputer.Core.Configuration;

public sealed record AssistantSettings
{
    public string WakePhrase { get; init; } = "Hey Computer";

    public bool StartAtLogin { get; init; }

    public bool HistoryEnabled { get; init; }

    public TimeSpan MaximumUtteranceDuration { get; init; } = TimeSpan.FromSeconds(30);

    public string PersonalityId { get; init; } = "helpful";

    public ProviderManifest? TranscriptionProvider { get; init; }

    public ProviderManifest? AgentProvider { get; init; }

    public ProviderManifest? SpeechProvider { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WakePhrase))
        {
            throw new ArgumentException("A wake phrase is required.", nameof(WakePhrase));
        }

        if (MaximumUtteranceDuration <= TimeSpan.Zero || MaximumUtteranceDuration > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumUtteranceDuration),
                "Maximum utterance duration must be between zero and two minutes.");
        }

        TranscriptionProvider?.Validate();
        AgentProvider?.Validate();
        SpeechProvider?.Validate();
    }
}
