namespace HeyComputer.Settings.Personality;

public sealed record EasterEggPolicies(string MaximumGoofiness)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MaximumGoofiness))
        {
            throw new InvalidDataException("The maximum-goofiness Easter-egg policy is required.");
        }

        if (MaximumGoofiness.Length > PersonalityStore.MaximumEasterEggPolicyLength)
        {
            throw new InvalidDataException("The maximum-goofiness Easter-egg policy is too long.");
        }
    }
}
