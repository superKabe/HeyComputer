using HeyComputer.Calibration;
using HeyComputer.Core.Credentials;
using HeyComputer.Onboarding.ViewModels;
using HeyComputer.Settings.Personality;

namespace HeyComputer.Onboarding.Tests;

public sealed class OnboardingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hey-computer-onboarding-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ProviderSecretIsStoredOnlyInCredentialStore()
    {
        var fixture = CreateFixture();

        var reference = await fixture.Coordinator.ConfigureProviderAsync("xai", "super-secret");

        Assert.Equal("provider:xai:api-key", reference);
        Assert.Equal("super-secret", await fixture.Credentials.GetAsync(reference));
        Assert.False(File.Exists(fixture.SettingsPath));
    }

    [Fact]
    public async Task FailedProviderTestDoesNotPersistSecret()
    {
        var fixture = CreateFixture(providerSucceeds: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Coordinator.ConfigureProviderAsync("xai", "bad-secret"));

        Assert.Null(await fixture.Credentials.GetAsync("provider:xai:api-key"));
    }

    [Fact]
    public async Task CompletePersistsOnlyMetadataAndNoRawAudio()
    {
        var fixture = CreateFixture();
        var reference = await fixture.Coordinator.ConfigureProviderAsync("xai", "secret");
        var receipt = CreateCalibrationReceipt("mic-1", passed: true);

        var completion = await fixture.Coordinator.CompleteAsync(
            true,
            "mic-1",
            "speaker-1",
            "xai",
            reference,
            receipt,
            true,
            true,
            DateTimeOffset.Parse("2026-10-21T19:00:00Z"));

        Assert.False(completion.RawAudioRetained);
        Assert.False(completion.ApiKeyRetainedInSettings);
        var json = await File.ReadAllTextAsync(fixture.SettingsPath);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audio", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(fixture.Startup.Enabled);
    }

    [Fact]
    public async Task CompletionRequiresConsentAndPassingCalibration()
    {
        var fixture = CreateFixture();
        var reference = await fixture.Coordinator.ConfigureProviderAsync("xai", "secret");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CompleteAsync(
            false, "mic-1", null, "xai", reference, CreateCalibrationReceipt("mic-1", true),
            false, false, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CompleteAsync(
            true, "mic-1", null, "xai", reference, CreateCalibrationReceipt("mic-1", false),
            false, false, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ResetRemovesSettingsCredentialPersonalityAndStartup()
    {
        var fixture = CreateFixture();
        var reference = await fixture.Coordinator.ConfigureProviderAsync("xai", "secret");
        await fixture.Coordinator.SavePersonalityAsync("Be extra direct.", true, true, true);
        await fixture.Coordinator.CompleteAsync(
            true, "mic-1", null, "xai", reference, CreateCalibrationReceipt("mic-1", true),
            true, false, DateTimeOffset.UtcNow);

        await fixture.Coordinator.ResetAsync("xai");

        Assert.False(File.Exists(fixture.SettingsPath));
        Assert.Null(await fixture.Credentials.GetAsync(reference));
        Assert.False(File.Exists(fixture.PersonalityPath));
        Assert.False(fixture.Startup.Enabled);
        Assert.True(fixture.Calibration.ResetCalled);
    }

    [Fact]
    public async Task WizardClearsApiKeyAfterSecureStorage()
    {
        var fixture = CreateFixture();
        var viewModel = new OnboardingWizardViewModel(fixture.Coordinator)
        {
            PrivacyConsentAccepted = true,
            MicrophoneId = "mic-1",
            ProviderId = "xai",
            ApiKey = "secret"
        };

        await viewModel.AdvanceAsync();
        await viewModel.AdvanceAsync();
        await viewModel.AdvanceAsync();

        Assert.Equal(OnboardingStep.WakeCalibration, viewModel.CurrentStep);
        Assert.Equal(string.Empty, viewModel.ApiKey);
        Assert.Equal("secret", await fixture.Credentials.GetAsync("provider:xai:api-key"));
    }

    [Fact]
    public async Task EasterEggMayBeSkipped()
    {
        var fixture = CreateFixture();

        var result = await fixture.Coordinator.ConfigureEasterEggAsync(null, DateTimeOffset.UtcNow);

        Assert.Null(result);
    }

    [Fact]
    public async Task JsonStoreRejectsSecretShapedUnknownFields()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """
        {
          "schemaVersion": 1,
          "privacyConsentAccepted": true,
          "microphoneId": "mic-1",
          "providerId": "xai",
          "credentialReference": "provider:xai:api-key",
          "launchAtSignIn": false,
          "setupCompleted": false,
          "calibrationPassed": false,
          "wakePhrase": "Hey Computer",
          "easterEggConfigured": false,
          "apiKey": "must-not-be-accepted"
        }
        """);

        var store = new JsonOnboardingSettingsStore(path);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    private Fixture CreateFixture(bool providerSucceeds = true)
    {
        Directory.CreateDirectory(_directory);
        var settingsPath = Path.Combine(_directory, "onboarding.json");
        var personalityPath = Path.Combine(_directory, "personality.json");
        var settingsStore = new JsonOnboardingSettingsStore(settingsPath);
        var credentials = new InMemoryCredentialStore();
        var calibrationState = new InMemoryCalibrationStateStore();
        var calibration = new FakeCalibrationSetupService();
        var startup = new FakeStartupService();
        var coordinator = new OnboardingCoordinator(
            settingsStore,
            credentials,
            new FakeDeviceSetupService(),
            new FakeProviderTester(providerSucceeds),
            calibration,
            startup,
            new PersonalityStore(personalityPath),
            new EasterEggTriggerService(calibrationState));
        return new Fixture(coordinator, credentials, calibration, startup, settingsPath, personalityPath);
    }

    private static CalibrationReceipt CreateCalibrationReceipt(string microphoneId, bool passed) => new(
        1,
        microphoneId,
        DateTimeOffset.UtcNow,
        passed,
        new CalibrationMetrics(7, 3, passed ? 7 : 2, passed ? 0 : 5, 0, 3, passed ? 1 : 2d / 7d, 0,
            TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(400)),
        "Hey Computer");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed record Fixture(
        OnboardingCoordinator Coordinator,
        InMemoryCredentialStore Credentials,
        FakeCalibrationSetupService Calibration,
        FakeStartupService Startup,
        string SettingsPath,
        string PersonalityPath);

    private sealed class FakeDeviceSetupService : IDeviceSetupService
    {
        public Task<DeviceTestResult> TestMicrophoneAsync(string microphoneId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeviceTestResult(true, "Microphone works."));

        public Task<DeviceTestResult> TestSpeakerAsync(string? speakerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeviceTestResult(true, "Speaker works."));
    }

    private sealed class FakeProviderTester(bool succeeds) : IProviderConnectionTester
    {
        public Task<ProviderTestResult> TestAsync(string providerId, string apiKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderTestResult(succeeds, succeeds ? "Connected." : "Connection failed."));
    }

    private sealed class FakeCalibrationSetupService : ICalibrationSetupService
    {
        public bool ResetCalled { get; private set; }

        public Task<CalibrationRunResult> RunAsync(string microphoneId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CalibrationRunResult(CreateCalibrationReceipt(microphoneId, true), "Calibration passed."));

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            ResetCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStartupService : ILaunchAtSignInService
    {
        public bool Enabled { get; private set; }

        public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            Enabled = enabled;
            return Task.CompletedTask;
        }
    }
}
