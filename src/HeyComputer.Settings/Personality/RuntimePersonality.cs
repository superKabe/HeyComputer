using HeyComputer.Core.Personality;

namespace HeyComputer.Settings.Personality;

public sealed record RuntimePersonality(
    PersonalityProfile Profile,
    EasterEggPolicies EasterEggs)
{
    public string? BuildResponseContext(PersonalityExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);
        return executionContext.MaximumGoofinessEasterEgg
            ? EasterEggs.MaximumGoofiness.Trim()
            : null;
    }

    public string ComposeInstructions(
        PersonalityPromptComposer composer,
        PersonalityExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(composer);
        return composer.Compose(Profile, BuildResponseContext(executionContext));
    }
}
