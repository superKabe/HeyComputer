namespace HeyComputer.Core.Personality;

public sealed record UserStyleAdaptation(
    bool Enabled = false,
    string? Pacing = null,
    string? Brevity = null,
    string? Vocabulary = null,
    string? Humor = null,
    string? ConversationalRhythm = null)
{
    public static UserStyleAdaptation Disabled { get; } = new();

    public UserStyleAdaptation Reset() => Disabled;

    public IReadOnlyDictionary<string, string> Inspect()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["enabled"] = Enabled.ToString()
        };

        AddIfPresent(values, "pacing", Pacing);
        AddIfPresent(values, "brevity", Brevity);
        AddIfPresent(values, "vocabulary", Vocabulary);
        AddIfPresent(values, "humor", Humor);
        AddIfPresent(values, "conversationalRhythm", ConversationalRhythm);
        return values;
    }

    internal IEnumerable<string> DescribeEnabledTraits()
    {
        if (!Enabled)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(Pacing))
        {
            yield return $"Pacing: {Pacing.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(Brevity))
        {
            yield return $"Brevity: {Brevity.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(Vocabulary))
        {
            yield return $"Vocabulary: {Vocabulary.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(Humor))
        {
            yield return $"Humor: {Humor.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(ConversationalRhythm))
        {
            yield return $"Conversational rhythm: {ConversationalRhythm.Trim()}";
        }
    }

    private static void AddIfPresent(IDictionary<string, string> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[key] = value.Trim();
        }
    }
}
