namespace HeyComputer.Core.Personality;

public sealed record PersonalityProfile(
    string Id,
    string DisplayName,
    string CorePrompt,
    int Version = 1,
    string? UserCustomization = null,
    bool AllowProfanity = false,
    bool PreferBriefSpeech = true,
    UserStyleAdaptation? StyleAdaptation = null)
{
    public static PersonalityProfile Helpful { get; } = new(
        "helpful",
        "Helpful",
        "Be practical, direct, warm, and honest. State uncertainty clearly.");

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("Personality ID and display name are required.");
        }

        if (string.IsNullOrWhiteSpace(CorePrompt))
        {
            throw new ArgumentException("A personality prompt is required.", nameof(CorePrompt));
        }

        if (Version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Version), "Version must be positive.");
        }
    }
}
