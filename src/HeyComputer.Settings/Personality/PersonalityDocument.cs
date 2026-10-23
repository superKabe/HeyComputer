using HeyComputer.Core.Personality;

namespace HeyComputer.Settings.Personality;

public sealed record PersonalityDocument(
    int SchemaVersion,
    PersonalityProfile Profile,
    EasterEggPolicies EasterEggs)
{
    public const int CurrentSchemaVersion = 1;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported personality schema version {SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        ArgumentNullException.ThrowIfNull(Profile);
        Profile.Validate();

        ArgumentNullException.ThrowIfNull(EasterEggs);
        EasterEggs.Validate();

        if (Profile.CorePrompt.Length > PersonalityStore.MaximumPromptLength)
        {
            throw new InvalidDataException("The personality prompt is too long.");
        }
    }
}
