namespace HeyComputer.Core.Providers;

public sealed record ProviderCapabilities(
    bool IsLocal,
    bool SupportsStreaming,
    bool SupportsCancellation,
    IReadOnlyCollection<int>? SupportedSampleRates = null,
    IReadOnlyCollection<string>? Voices = null,
    bool SupportsTools = false);
