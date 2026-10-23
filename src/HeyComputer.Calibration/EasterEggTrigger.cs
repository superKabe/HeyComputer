using System.Text.RegularExpressions;

namespace HeyComputer.Calibration;

public sealed record EasterEggTriggerMetadata(
    int SchemaVersion,
    string NormalizedPhrase,
    DateTimeOffset UpdatedAt,
    bool Enabled = true,
    bool AudioRetained = false);

public sealed record EasterEggValidationResult(bool IsValid, string? NormalizedPhrase, IReadOnlyList<string> Reasons);

public sealed record EasterEggMatchResult(bool Matched, string? TriggerId = null)
{
    public static EasterEggMatchResult NoMatch { get; } = new(false);
}

public interface IEasterEggTriggerMatcher
{
    EasterEggMatchResult Match(string recognizedText, EasterEggTriggerMetadata? configuredTrigger);
}

public sealed partial class EasterEggTriggerService(ICalibrationStateStore stateStore) : IEasterEggTriggerMatcher
{
    private static readonly string[] OrdinaryCommands =
    [
        "stop", "cancel", "mute", "unmute", "help", "listen", "start listening", "stop listening",
        "turn on", "turn off", "what time is it", "hey computer"
    ];

    public EasterEggValidationResult Validate(string? phrase)
    {
        var normalized = Normalize(phrase);
        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            reasons.Add("Choose a phrase or skip this optional step.");
            return new(false, null, reasons);
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is < 2 or > 8)
        {
            reasons.Add("Use a distinctive phrase between two and eight words.");
        }

        if (normalized.Length < 6)
        {
            reasons.Add("The phrase is too short to be distinctive.");
        }

        if (OrdinaryCommands.Any(command => Similarity(normalized, command) >= 0.72))
        {
            reasons.Add("Choose a phrase more distinct from Hey Computer and ordinary commands.");
        }

        return new(reasons.Count == 0, normalized, reasons);
    }

    public async Task<EasterEggTriggerMetadata> ConfigureAsync(
        string phrase,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(phrase);
        if (!validation.IsValid || validation.NormalizedPhrase is null)
        {
            throw new ArgumentException(string.Join(" ", validation.Reasons), nameof(phrase));
        }

        var metadata = new EasterEggTriggerMetadata(1, validation.NormalizedPhrase, updatedAt);
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        await stateStore.SaveAsync(state with { EasterEggTrigger = metadata }, cancellationToken).ConfigureAwait(false);
        return metadata;
    }

    public async Task SkipOrResetAsync(CancellationToken cancellationToken = default)
    {
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        await stateStore.SaveAsync(state with { EasterEggTrigger = null }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (state.EasterEggTrigger is null)
        {
            throw new InvalidOperationException("No Easter-egg trigger is configured.");
        }

        await stateStore.SaveAsync(
            state with { EasterEggTrigger = state.EasterEggTrigger with { Enabled = enabled } },
            cancellationToken).ConfigureAwait(false);
    }

    public EasterEggMatchResult Match(string recognizedText, EasterEggTriggerMetadata? configuredTrigger)
    {
        if (configuredTrigger is not { Enabled: true })
        {
            return EasterEggMatchResult.NoMatch;
        }

        var recognized = Normalize(recognizedText);
        return string.Equals(recognized, configuredTrigger.NormalizedPhrase, StringComparison.Ordinal)
            ? new EasterEggMatchResult(true, "custom-easter-egg")
            : EasterEggMatchResult.NoMatch;
    }

    private static string Normalize(string? phrase) =>
        WhitespaceRegex().Replace(NonWordRegex().Replace((phrase ?? string.Empty).Trim().ToLowerInvariant(), " "), " ").Trim();

    private static double Similarity(string left, string right)
    {
        if (left == right)
        {
            return 1;
        }

        var distance = Levenshtein(left, right);
        return 1d - (double)distance / Math.Max(left.Length, right.Length);
    }

    private static int Levenshtein(string left, string right)
    {
        var costs = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var previous = costs[0];
            costs[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var current = costs[j];
                costs[j] = Math.Min(
                    Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    previous + (left[i - 1] == right[j - 1] ? 0 : 1));
                previous = current;
            }
        }

        return costs[^1];
    }

    [GeneratedRegex(@"[^\p{L}\p{N}']+")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
