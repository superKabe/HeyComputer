using HeyComputer.Calibration;

namespace HeyComputer.Onboarding.ViewModels;

public sealed class OnboardingWizardViewModel(OnboardingCoordinator coordinator) : ObservableObject
{
    private readonly OnboardingCoordinator _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private OnboardingStep _currentStep = OnboardingStep.Privacy;
    private bool _privacyConsentAccepted;
    private string _microphoneId = string.Empty;
    private string? _speakerId;
    private string _providerId = "xai";
    private string _apiKey = string.Empty;
    private string? _credentialReference;
    private CalibrationReceipt? _calibrationReceipt;
    private string? _personalityCustomization;
    private bool _allowProfanity = true;
    private bool _preferBriefSpeech = true;
    private bool _enableStyleAdaptation = true;
    private string? _easterEggPhrase;
    private bool _easterEggConfigured;
    private bool _launchAtSignIn;
    private bool _isBusy;
    private string _status = "Review the privacy summary to begin.";
    private OnboardingCompletionReceipt? _completionReceipt;

    public OnboardingStep CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(IsComplete));
            }
        }
    }

    public bool PrivacyConsentAccepted { get => _privacyConsentAccepted; set => SetProperty(ref _privacyConsentAccepted, value); }
    public string MicrophoneId { get => _microphoneId; set => SetProperty(ref _microphoneId, value); }
    public string? SpeakerId { get => _speakerId; set => SetProperty(ref _speakerId, value); }
    public string ProviderId { get => _providerId; set => SetProperty(ref _providerId, value); }
    public string ApiKey { get => _apiKey; set => SetProperty(ref _apiKey, value); }
    public string? PersonalityCustomization { get => _personalityCustomization; set => SetProperty(ref _personalityCustomization, value); }
    public bool AllowProfanity { get => _allowProfanity; set => SetProperty(ref _allowProfanity, value); }
    public bool PreferBriefSpeech { get => _preferBriefSpeech; set => SetProperty(ref _preferBriefSpeech, value); }
    public bool EnableStyleAdaptation { get => _enableStyleAdaptation; set => SetProperty(ref _enableStyleAdaptation, value); }
    public string? EasterEggPhrase { get => _easterEggPhrase; set => SetProperty(ref _easterEggPhrase, value); }
    public bool LaunchAtSignIn { get => _launchAtSignIn; set => SetProperty(ref _launchAtSignIn, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool CanGoBack => CurrentStep is not OnboardingStep.Privacy and not OnboardingStep.Complete;
    public bool IsComplete => CurrentStep == OnboardingStep.Complete;
    public OnboardingCompletionReceipt? CompletionReceipt => _completionReceipt;

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        CurrentStep--;
        Status = "Review or change this step.";
    }

    public async Task AdvanceAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || IsComplete)
        {
            return;
        }

        IsBusy = true;
        try
        {
            switch (CurrentStep)
            {
                case OnboardingStep.Privacy:
                    if (!PrivacyConsentAccepted)
                    {
                        throw new InvalidOperationException("Accept the microphone privacy summary to continue.");
                    }

                    CurrentStep = OnboardingStep.Devices;
                    Status = "Test the selected microphone and speaker.";
                    break;
                case OnboardingStep.Devices:
                    var microphone = await _coordinator.TestMicrophoneAsync(MicrophoneId, cancellationToken);
                    if (!microphone.Succeeded)
                    {
                        throw new InvalidOperationException(microphone.Message);
                    }

                    var speaker = await _coordinator.TestSpeakerAsync(SpeakerId, cancellationToken);
                    if (!speaker.Succeeded)
                    {
                        throw new InvalidOperationException(speaker.Message);
                    }

                    CurrentStep = OnboardingStep.Provider;
                    Status = "Connect an AI provider. The API key is stored in Windows credentials.";
                    break;
                case OnboardingStep.Provider:
                    _credentialReference = await _coordinator.ConfigureProviderAsync(ProviderId, ApiKey, cancellationToken);
                    ApiKey = string.Empty;
                    CurrentStep = OnboardingStep.WakeCalibration;
                    Status = "Run the guided Hey Computer calibration.";
                    break;
                case OnboardingStep.WakeCalibration:
                    var calibration = await _coordinator.RunCalibrationAsync(MicrophoneId, cancellationToken);
                    _calibrationReceipt = calibration.Receipt;
                    if (!_calibrationReceipt.Passed)
                    {
                        throw new InvalidOperationException(calibration.Summary);
                    }

                    CurrentStep = OnboardingStep.Personality;
                    Status = "Choose how the assistant should speak.";
                    break;
                case OnboardingStep.Personality:
                    await _coordinator.SavePersonalityAsync(
                        PersonalityCustomization,
                        AllowProfanity,
                        PreferBriefSpeech,
                        EnableStyleAdaptation,
                        cancellationToken);
                    CurrentStep = OnboardingStep.EasterEgg;
                    Status = "Optionally choose a private phrase for maximum goofiness.";
                    break;
                case OnboardingStep.EasterEgg:
                    var easterEgg = await _coordinator.ConfigureEasterEggAsync(
                        EasterEggPhrase,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                    _easterEggConfigured = easterEgg is not null;
                    CurrentStep = OnboardingStep.Startup;
                    Status = "Choose whether Hey Computer starts when you sign in.";
                    break;
                case OnboardingStep.Startup:
                    CurrentStep = OnboardingStep.Review;
                    Status = "Review the setup before saving.";
                    break;
                case OnboardingStep.Review:
                    _completionReceipt = await _coordinator.CompleteAsync(
                        PrivacyConsentAccepted,
                        MicrophoneId,
                        SpeakerId,
                        ProviderId,
                        _credentialReference ?? throw new InvalidOperationException("Provider setup is incomplete."),
                        _calibrationReceipt ?? throw new InvalidOperationException("Calibration is incomplete."),
                        LaunchAtSignIn,
                        _easterEggConfigured,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                    OnPropertyChanged(nameof(CompletionReceipt));
                    CurrentStep = OnboardingStep.Complete;
                    Status = "Setup is complete. No raw calibration audio was retained.";
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Setup step canceled.";
            throw;
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _coordinator.ResetAsync(ProviderId, cancellationToken);
        _credentialReference = null;
        _calibrationReceipt = null;
        _completionReceipt = null;
        ApiKey = string.Empty;
        EasterEggPhrase = null;
        _easterEggConfigured = false;
        PrivacyConsentAccepted = false;
        CurrentStep = OnboardingStep.Privacy;
        OnPropertyChanged(nameof(CompletionReceipt));
        Status = "Setup was reset. Review the privacy summary to begin again.";
    }
}
