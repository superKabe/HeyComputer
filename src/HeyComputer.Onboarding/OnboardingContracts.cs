namespace HeyComputer.Onboarding;

public interface IOnboardingSettingsStore
{
    Task<OnboardingSettings?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(OnboardingSettings settings, CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}

public interface IDeviceSetupService
{
    Task<DeviceTestResult> TestMicrophoneAsync(string microphoneId, CancellationToken cancellationToken = default);

    Task<DeviceTestResult> TestSpeakerAsync(string? speakerId, CancellationToken cancellationToken = default);
}

public interface IProviderConnectionTester
{
    Task<ProviderTestResult> TestAsync(
        string providerId,
        string apiKey,
        CancellationToken cancellationToken = default);
}

public interface ICalibrationSetupService
{
    Task<CalibrationRunResult> RunAsync(string microphoneId, CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}

public interface ILaunchAtSignInService
{
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
