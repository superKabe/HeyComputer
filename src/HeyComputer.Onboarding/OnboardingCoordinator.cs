using HeyComputer.Calibration;
using HeyComputer.Core.Credentials;
using HeyComputer.Core.Personality;
using HeyComputer.Settings.Personality;

namespace HeyComputer.Onboarding;

public sealed class OnboardingCoordinator
{
    private const string CredentialPrefix = "provider:";
    private const string CredentialSuffix = ":api-key";

    private readonly IOnboardingSettingsStore _settingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly IDeviceSetupService _deviceSetup;
    private readonly IProviderConnectionTester _providerTester;
    private readonly ICalibrationSetupService _calibration;
    private readonly ILaunchAtSignInService _startup;
    private readonly PersonalityStore _personalityStore;
    private readonly EasterEggTriggerService _easterEggs;

    public OnboardingCoordinator(
        IOnboardingSettingsStore settingsStore,
        ICredentialStore credentialStore,
        IDeviceSetupService deviceSetup,
        IProviderConnectionTester providerTester,
        ICalibrationSetupService calibration,
        ILaunchAtSignInService startup,
        PersonalityStore personalityStore,
        EasterEggTriggerService easterEggs)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _deviceSetup = deviceSetup ?? throw new ArgumentNullException(nameof(deviceSetup));
        _providerTester = providerTester ?? throw new ArgumentNullException(nameof(providerTester));
        _calibration = calibration ?? throw new ArgumentNullException(nameof(calibration));
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _personalityStore = personalityStore ?? throw new ArgumentNullException(nameof(personalityStore));
        _easterEggs = easterEggs ?? throw new ArgumentNullException(nameof(easterEggs));
    }

    public Task<DeviceTestResult> TestMicrophoneAsync(
        string microphoneId,
        CancellationToken cancellationToken = default) =>
        _deviceSetup.TestMicrophoneAsync(RequireValue(microphoneId, nameof(microphoneId)), cancellationToken);

    public Task<DeviceTestResult> TestSpeakerAsync(
        string? speakerId,
        CancellationToken cancellationToken = default) =>
        _deviceSetup.TestSpeakerAsync(speakerId?.Trim(), cancellationToken);

    public async Task<string> ConfigureProviderAsync(
        string providerId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = RequireValue(providerId, nameof(providerId)).ToLowerInvariant();
        var secret = RequireValue(apiKey, nameof(apiKey));
        var result = await _providerTester.TestAsync(normalizedProvider, secret, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message);
        }

        var credentialReference = CredentialPrefix + normalizedProvider + CredentialSuffix;
        await _credentialStore.SetAsync(credentialReference, secret, cancellationToken).ConfigureAwait(false);
        return credentialReference;
    }

    public Task<CalibrationRunResult> RunCalibrationAsync(
        string microphoneId,
        CancellationToken cancellationToken = default) =>
        _calibration.RunAsync(RequireValue(microphoneId, nameof(microphoneId)), cancellationToken);

    public Task<PersonalityProfile> SavePersonalityAsync(
        string? userCustomization,
        bool allowProfanity,
        bool preferBriefSpeech,
        bool enableStyleAdaptation,
        CancellationToken cancellationToken = default) =>
        _personalityStore.SaveOverrideAsync(
            new PersonalityOverride
            {
                UserCustomization = Normalize(userCustomization),
                AllowProfanity = allowProfanity,
                PreferBriefSpeech = preferBriefSpeech,
                StyleAdaptation = enableStyleAdaptation
                    ? new UserStyleAdaptation(
                        Enabled: true,
                        Pacing: "Adapt gently to the user's pace.",
                        Brevity: "Match the user's approximate response length.",
                        Vocabulary: "Mirror vocabulary level without copying distinctive phrases.",
                        Humor: "Match humor subtly without caricature.",
                        ConversationalRhythm: "Follow conversational rhythm without impersonation.")
                    : UserStyleAdaptation.Disabled
            },
            cancellationToken);

    public async Task<EasterEggTriggerMetadata?> ConfigureEasterEggAsync(
        string? phrase,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            await _easterEggs.SkipOrResetAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        return await _easterEggs.ConfigureAsync(phrase, updatedAt, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OnboardingCompletionReceipt> CompleteAsync(
        bool privacyConsentAccepted,
        string microphoneId,
        string? speakerId,
        string providerId,
        string credentialReference,
        CalibrationReceipt calibrationReceipt,
        bool launchAtSignIn,
        bool easterEggConfigured,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        if (!privacyConsentAccepted)
        {
            throw new InvalidOperationException("Privacy consent is required before setup can complete.");
        }

        ArgumentNullException.ThrowIfNull(calibrationReceipt);
        if (!calibrationReceipt.Passed)
        {
            throw new InvalidOperationException("Wake-word calibration must pass before setup can complete.");
        }

        var normalizedMicrophone = RequireValue(microphoneId, nameof(microphoneId));
        if (!string.Equals(normalizedMicrophone, calibrationReceipt.MicrophoneId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Calibration does not match the selected microphone.");
        }

        var normalizedProvider = RequireValue(providerId, nameof(providerId)).ToLowerInvariant();
        var normalizedCredentialReference = RequireValue(credentialReference, nameof(credentialReference));
        if (!normalizedCredentialReference.StartsWith(CredentialPrefix + normalizedProvider + ":", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The credential reference does not match the selected provider.");
        }

        await _startup.SetEnabledAsync(launchAtSignIn, cancellationToken).ConfigureAwait(false);
        var settings = new OnboardingSettings(
            OnboardingSettings.CurrentSchemaVersion,
            true,
            normalizedMicrophone,
            Normalize(speakerId),
            normalizedProvider,
            normalizedCredentialReference,
            launchAtSignIn,
            true,
            completedAt,
            true,
            calibrationReceipt.WakePhrase,
            easterEggConfigured);
        await _settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);

        return new OnboardingCompletionReceipt(
            1,
            completedAt,
            normalizedMicrophone,
            normalizedProvider,
            calibrationReceipt.WakePhrase,
            true,
            launchAtSignIn,
            easterEggConfigured);
    }

    public async Task ResetAsync(
        string? providerId,
        CancellationToken cancellationToken = default)
    {
        await _settingsStore.ResetAsync(cancellationToken).ConfigureAwait(false);
        await _calibration.ResetAsync(cancellationToken).ConfigureAwait(false);
        await _personalityStore.ResetAsync(cancellationToken).ConfigureAwait(false);
        await _easterEggs.SkipOrResetAsync(cancellationToken).ConfigureAwait(false);
        await _startup.SetEnabledAsync(false, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(providerId))
        {
            await _credentialStore.DeleteAsync(
                    CredentialPrefix + providerId.Trim().ToLowerInvariant() + CredentialSuffix,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
