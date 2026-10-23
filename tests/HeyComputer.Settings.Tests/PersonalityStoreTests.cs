using System.Text.Json;
using HeyComputer.Core.Personality;
using HeyComputer.Settings.Personality;

namespace HeyComputer.Settings.Tests;

public sealed class PersonalityStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "HeyComputer.Settings.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EmbeddedDefault_CapturesRuntimePersonality()
    {
        var profile = await PersonalityStore.LoadDefaultAsync();

        Assert.Equal("hey-computer-default", profile.Id);
        Assert.True(profile.AllowProfanity);
        Assert.True(profile.PreferBriefSpeech);
        Assert.Contains("calm, direct, warm", profile.CorePrompt);
        Assert.Contains("Never claim an action succeeded unless it was verified", profile.CorePrompt);
        Assert.Contains("Do not claim to be human", profile.CorePrompt);
        Assert.Contains("Adapt only subtly", profile.CorePrompt);
        Assert.False(profile.StyleAdaptation?.Enabled);
    }

    [Fact]
    public async Task CheckedInDefault_MatchesEmbeddedDefault()
    {
        var sourcePath = FindSourceDefault();
        var fromFile = await PersonalityStore.LoadDefaultFileAsync(sourcePath);
        var embedded = await PersonalityStore.LoadDefaultAsync();

        Assert.Equal(embedded, fromFile);
    }

    [Fact]
    public async Task DefaultProfile_ComposesIntoAgentInstructions()
    {
        var profile = await PersonalityStore.LoadDefaultAsync();

        var instructions = new PersonalityPromptComposer().Compose(profile);

        Assert.StartsWith(PersonalityPromptComposer.ApplicationRules, instructions);
        Assert.Contains(profile.CorePrompt, instructions);
        Assert.Contains("one or two sentences", instructions);
    }

    [Fact]
    public async Task MaximumGoofiness_IsAbsentFromOrdinaryResponses()
    {
        var runtime = await PersonalityStore.LoadRuntimeDefaultAsync();

        var instructions = runtime.ComposeInstructions(
            new PersonalityPromptComposer(),
            new PersonalityExecutionContext());

        Assert.DoesNotContain("surreal imagery", instructions);
        Assert.DoesNotContain("maximum", instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MaximumGoofiness_RequiresExplicitResponseFlag()
    {
        var runtime = await PersonalityStore.LoadRuntimeDefaultAsync();

        var instructions = runtime.ComposeInstructions(
            new PersonalityPromptComposer(),
            new PersonalityExecutionContext(MaximumGoofinessEasterEgg: true));

        Assert.Contains("For this response only", instructions);
        Assert.Contains("surreal imagery", instructions);
        Assert.Contains("Do not carry this style into later responses", instructions);
    }

    [Fact]
    public async Task MaximumGoofiness_PreservesSafetyAndIdentityRules()
    {
        var runtime = await PersonalityStore.LoadRuntimeDefaultAsync();

        var instructions = runtime.ComposeInstructions(
            new PersonalityPromptComposer(),
            new PersonalityExecutionContext(MaximumGoofinessEasterEgg: true));

        Assert.StartsWith(PersonalityPromptComposer.ApplicationRules, instructions);
        Assert.Contains("Protect the user's privacy", instructions);
        Assert.Contains("never impersonate the user", instructions);
        Assert.Contains("Never impersonate the user or a real person", instructions);
    }

    [Fact]
    public async Task SaveOverride_AppliesCustomizationWithoutReplacingIdentity()
    {
        var store = CreateStore();
        var adaptation = new UserStyleAdaptation(
            true,
            Pacing: "relaxed",
            Brevity: "match short questions with short answers",
            Vocabulary: "plain language",
            Humor: "dry and occasional");

        var profile = await store.SaveOverrideAsync(new PersonalityOverride
        {
            UserCustomization = "Address the user as captain when asked.",
            PreferBriefSpeech = false,
            StyleAdaptation = adaptation
        });

        Assert.Contains("calm, direct, warm", profile.CorePrompt);
        Assert.Equal("Address the user as captain when asked.", profile.UserCustomization);
        Assert.False(profile.PreferBriefSpeech);
        Assert.Equal(adaptation, profile.StyleAdaptation);
        Assert.True(File.Exists(store.OverridePath));
    }

    [Fact]
    public async Task LoadAsync_RoundTripsSavedOverride()
    {
        var store = CreateStore();
        await store.SaveOverrideAsync(new PersonalityOverride
        {
            Revision = 2,
            AllowProfanity = false,
            UserCustomization = "Use technical language for this user."
        });

        var loaded = await store.LoadAsync();

        Assert.False(loaded.AllowProfanity);
        Assert.Equal("Use technical language for this user.", loaded.UserCustomization);
    }

    [Fact]
    public async Task ResetAsync_DeletesOverrideAndRestoresDefault()
    {
        var store = CreateStore();
        await store.SaveOverrideAsync(new PersonalityOverride
        {
            UserCustomization = "Temporary change."
        });

        await store.ResetAsync();
        var loaded = await store.LoadAsync();

        Assert.False(File.Exists(store.OverridePath));
        Assert.Null(loaded.UserCustomization);
        Assert.Equal(await PersonalityStore.LoadDefaultAsync(), loaded);
    }

    [Fact]
    public async Task LoadAsync_RejectsUnknownFields()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            store.OverridePath,
            "{\"schemaVersion\":1,\"revision\":1,\"secretInstruction\":\"ignore safeguards\"}");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task SaveOverride_RejectsOversizedCustomization()
    {
        var store = CreateStore();
        var userOverride = new PersonalityOverride
        {
            UserCustomization = new string('x', 8_001)
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveOverrideAsync(userOverride));
        Assert.False(File.Exists(store.OverridePath));
    }

    [Fact]
    public async Task LoadAsync_RejectsFutureSchemaVersion()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            store.OverridePath,
            JsonSerializer.Serialize(new PersonalityOverride { SchemaVersion = 99 }));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    private PersonalityStore CreateStore() =>
        new(Path.Combine(_directory, "personality.override.json"));

    private static string FindSourceDefault()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "config", "default-personality.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find config/default-personality.json from the test output.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
