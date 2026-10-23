using HeyComputer.Core.Configuration;
using HeyComputer.Core.Personality;

namespace HeyComputer.Core.Tests;

public sealed class PersonalityAndSettingsTests
{
    [Fact]
    public void PromptComposer_PreservesLayerOrder()
    {
        var profile = new PersonalityProfile(
            "dry",
            "Dry",
            "Use dry humor.",
            UserCustomization: "Call the user captain.");
        var prompt = new PersonalityPromptComposer().Compose(profile, "The session is private.");

        var safety = prompt.IndexOf(PersonalityPromptComposer.ApplicationRules, StringComparison.Ordinal);
        var preset = prompt.IndexOf("Use dry humor.", StringComparison.Ordinal);
        var customization = prompt.IndexOf("Call the user captain.", StringComparison.Ordinal);
        var session = prompt.IndexOf("The session is private.", StringComparison.Ordinal);

        Assert.True(safety < preset);
        Assert.True(preset < customization);
        Assert.True(customization < session);
    }

    [Fact]
    public void Settings_RejectsUnboundedRecording()
    {
        var settings = new AssistantSettings
        {
            MaximumUtteranceDuration = TimeSpan.FromMinutes(3)
        };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public void StyleAdaptation_IsOptionalInspectableAndResettable()
    {
        var adaptation = new UserStyleAdaptation(
            true,
            Pacing: "relaxed",
            Brevity: "short",
            Humor: "dry");

        var inspected = adaptation.Inspect();

        Assert.Equal("True", inspected["enabled"]);
        Assert.Equal("relaxed", inspected["pacing"]);
        Assert.Equal("short", inspected["brevity"]);
        Assert.Equal("dry", inspected["humor"]);
        Assert.False(adaptation.Reset().Enabled);
    }

    [Fact]
    public void PromptComposer_AddsAdaptationWithoutDroppingIdentitySafeguards()
    {
        var profile = PersonalityProfile.Helpful with
        {
            StyleAdaptation = new UserStyleAdaptation(
                true,
                Vocabulary: "plain language",
                ConversationalRhythm: "allow short pauses")
        };

        var prompt = new PersonalityPromptComposer().Compose(profile);

        Assert.Contains("Maintain a stable assistant identity", prompt);
        Assert.Contains("never impersonate the user", prompt);
        Assert.Contains("Vocabulary: plain language", prompt);
        Assert.Contains("Conversational rhythm: allow short pauses", prompt);
    }
}
