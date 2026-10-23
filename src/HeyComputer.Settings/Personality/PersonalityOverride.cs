using HeyComputer.Core.Personality;

namespace HeyComputer.Settings.Personality;

public sealed record PersonalityOverride
{
    public int SchemaVersion { get; init; } = PersonalityDocument.CurrentSchemaVersion;

    public int Revision { get; init; } = 1;

    public string? UserCustomization { get; init; }

    public bool? AllowProfanity { get; init; }

    public bool? PreferBriefSpeech { get; init; }

    public UserStyleAdaptation? StyleAdaptation { get; init; }

    public void Validate()
    {
        if (SchemaVersion != PersonalityDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported personality override schema version {SchemaVersion}.");
        }

        if (Revision < 1)
        {
            throw new InvalidDataException("Personality override revision must be positive.");
        }

        if (UserCustomization?.Length > PersonalityStore.MaximumCustomizationLength)
        {
            throw new InvalidDataException("The personality customization is too long.");
        }

        ValidateTrait(StyleAdaptation?.Pacing, "pacing");
        ValidateTrait(StyleAdaptation?.Brevity, "brevity");
        ValidateTrait(StyleAdaptation?.Vocabulary, "vocabulary");
        ValidateTrait(StyleAdaptation?.Humor, "humor");
        ValidateTrait(StyleAdaptation?.ConversationalRhythm, "conversational rhythm");
    }

    public PersonalityProfile ApplyTo(PersonalityProfile baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        Validate();

        var profile = baseline with
        {
            UserCustomization = Normalize(UserCustomization),
            AllowProfanity = AllowProfanity ?? baseline.AllowProfanity,
            PreferBriefSpeech = PreferBriefSpeech ?? baseline.PreferBriefSpeech,
            StyleAdaptation = StyleAdaptation ?? baseline.StyleAdaptation
        };

        profile.Validate();
        return profile;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateTrait(string? value, string name)
    {
        if (value?.Length > PersonalityStore.MaximumTraitLength)
        {
            throw new InvalidDataException($"The {name} adaptation is too long.");
        }
    }
}
