using System.IO;
using HeyComputer.Calibration;

namespace HeyComputer.Onboarding;

public sealed record OnboardingSettings(
    int SchemaVersion,
    bool PrivacyConsentAccepted,
    string MicrophoneId,
    string? SpeakerId,
    string ProviderId,
    string CredentialReference,
    bool LaunchAtSignIn,
    bool SetupCompleted,
    DateTimeOffset? CompletedAt,
    bool CalibrationPassed,
    string WakePhrase,
    bool EasterEggConfigured)
{
    public const int CurrentSchemaVersion = 1;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported onboarding schema version {SchemaVersion}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(MicrophoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CredentialReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(WakePhrase);

        if (SetupCompleted && (!PrivacyConsentAccepted || !CalibrationPassed || CompletedAt is null))
        {
            throw new InvalidDataException("Completed setup requires consent and a passing calibration receipt.");
        }
    }
}

public sealed record OnboardingCompletionReceipt(
    int SchemaVersion,
    DateTimeOffset CompletedAt,
    string MicrophoneId,
    string ProviderId,
    string WakePhrase,
    bool CalibrationPassed,
    bool LaunchAtSignIn,
    bool EasterEggConfigured,
    bool RawAudioRetained = false,
    bool ApiKeyRetainedInSettings = false);

public sealed record DeviceTestResult(bool Succeeded, string Message);

public sealed record ProviderTestResult(bool Succeeded, string Message);

public sealed record CalibrationRunResult(CalibrationReceipt Receipt, string Summary);
