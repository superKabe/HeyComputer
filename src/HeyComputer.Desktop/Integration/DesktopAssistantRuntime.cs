using System.Runtime.CompilerServices;
using System.Net.Http;
using System.IO;
using HeyComputer.Audio;
using HeyComputer.Core.Audio;
using HeyComputer.Core.Conversation;
using HeyComputer.Core.Credentials;
using HeyComputer.Core.Personality;
using HeyComputer.Core.Providers;
using HeyComputer.Desktop.Audio;
using HeyComputer.Providers.Xai;
using HeyComputer.Providers.Xai.Audio;
using HeyComputer.Providers.Xai.Speech;
using HeyComputer.Providers.Xai.Text;
using HeyComputer.Settings.Personality;
using HeyComputer.WakeWord;
using HeyComputer.WakeWord.SherpaOnnx;
using CoreAssistantState = HeyComputer.Core.Conversation.AssistantState;

namespace HeyComputer.Desktop.Integration;

public sealed record DesktopReadiness(
    bool ApiKeyConfigured,
    bool WakeModelConfigured,
    string Summary);

public sealed class DesktopAssistantRuntime : IAsyncDisposable
{
    private readonly IAudioCaptureService _capture;
    private readonly BoundedAudioFrameChannel _frames = new(96);
    private readonly WindowsCredentialStore _credentialStore = new();
    private readonly CredentialStoreXaiApiKeyProvider _apiKeyProvider;
    private readonly PersonalityStore _personalityStore;
    private readonly PersonalityPromptComposer _promptComposer = new();
    private readonly WaveOutAudioPlaybackSink _playback = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly SemaphoreSlim _interactionGate = new(1, 1);
    private readonly object _operationSync = new();
    private RuntimePersonality? _personality;
    private StreamingWakeWordDetector? _wakeDetector;
    private ConversationCoordinator? _coordinator;
    private CancellationTokenSource? _wakeLoopCancellation;
    private CancellationTokenSource? _activeManualTurn;
    private Task? _wakeLoop;
    private bool _listening;
    private bool _disposed;
    private bool _reportedAudioFormatError;

    public DesktopAssistantRuntime(IAudioCaptureService capture)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _apiKeyProvider = new CredentialStoreXaiApiKeyProvider(_credentialStore);
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeyComputer");
        _personalityStore = new PersonalityStore(Path.Combine(settingsDirectory, "personality.override.json"));
        _capture.FrameAvailable += OnFrameAvailable;
    }

    public event EventHandler<AssistantState>? StateChanged;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<DesktopReadiness>? ReadinessChanged;

    public DesktopReadiness Readiness { get; private set; } =
        new(false, false, "Loading local configuration…");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _personality = await _personalityStore.LoadRuntimeAsync(cancellationToken).ConfigureAwait(false);

        var modelStatus = "Wake model is not installed; use push-to-talk or microphone test mode.";
        var manifestPath = FindWakeModelManifest();
        if (manifestPath is not null)
        {
            try
            {
                var runtime = await SherpaOnnxKeywordSpotterRuntime
                    .CreateAsync(manifestPath, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                _wakeDetector = new StreamingWakeWordDetector(runtime);
                modelStatus = "Local “Hey Computer” detection is ready.";
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException)
            {
                modelStatus = "The installed wake model failed validation; wake listening remains disabled.";
            }
        }

        _coordinator = CreateCoordinator();
        _coordinator.StateMachine.StateChanged += OnCoreStateChanged;
        var hasKey = await _apiKeyProvider.IsConfiguredAsync(cancellationToken).ConfigureAwait(false);
        SetReadiness(hasKey, _wakeDetector is not null, modelStatus);
    }

    public async Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        await _credentialStore
            .SetAsync(CredentialStoreXaiApiKeyProvider.CredentialKey, apiKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        SetReadiness(true, _wakeDetector is not null, BuildReadinessSummary(true, _wakeDetector is not null));
        PublishStatus("xAI access is configured in Windows Credential Manager.");
    }

    public async Task RemoveApiKeyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _credentialStore
            .DeleteAsync(CredentialStoreXaiApiKeyProvider.CredentialKey, cancellationToken)
            .ConfigureAwait(false);
        SetReadiness(false, _wakeDetector is not null, BuildReadinessSummary(false, _wakeDetector is not null));
        PublishStatus("The xAI credential was removed from Windows Credential Manager.");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _capture.StartAsync(cancellationToken).ConfigureAwait(false);
        _listening = true;
        _coordinator?.Unmute();
        PublishState(AssistantState.Waiting);

        if (_wakeDetector is null)
        {
            PublishStatus("Microphone test is active locally. Install a validated wake model to enable “Hey Computer”.");
            return;
        }

        PublishStatus("Listening locally for “Hey Computer”.");
        StartWakeLoop();
    }

    public async Task RunPushToTalkAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_capture.IsCapturing)
        {
            await _capture.StartAsync(cancellationToken).ConfigureAwait(false);
            _listening = true;
        }

        await PauseWakeLoopAsync().ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_operationSync)
        {
            _activeManualTurn = linked;
        }

        try
        {
            await RunTurnAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            lock (_operationSync)
            {
                if (ReferenceEquals(_activeManualTurn, linked))
                {
                    _activeManualTurn = null;
                }
            }

            if (_listening && _wakeDetector is not null && !_disposed)
            {
                StartWakeLoop();
            }
        }
    }

    public async Task MuteAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _listening = false;
        CancelManualTurn();
        await PauseWakeLoopAsync().ConfigureAwait(false);
        await WaitForInteractionAsync().ConfigureAwait(false);
        _coordinator?.Mute();
        _playback.Stop();
        await _capture.StopAsync(cancellationToken).ConfigureAwait(false);
        PublishState(AssistantState.Muted);
        PublishStatus("Microphone capture is completely stopped.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _listening = false;
        CancelManualTurn();
        await PauseWakeLoopAsync().ConfigureAwait(false);
        await WaitForInteractionAsync().ConfigureAwait(false);
        _coordinator?.CancelActiveTurn();
        _playback.Stop();
        await _capture.StopAsync(cancellationToken).ConfigureAwait(false);
        PublishState(AssistantState.Sleeping);
        PublishStatus("No microphone audio is being captured.");
    }

    private ConversationCoordinator CreateCoordinator()
    {
        var options = new XaiProviderOptions();
        var transcription = new XaiTranscriptionProvider(_httpClient, _apiKeyProvider, options);
        var agent = new XaiAgentProvider(_httpClient, _apiKeyProvider, options);
        var speech = new XaiStreamingTextToSpeechProvider(
            _apiKeyProvider,
            new ClientWebSocketTtsSessionFactory(),
            _playback,
            options);
        return new ConversationCoordinator(
            (IWakeWordDetector?)_wakeDetector ?? new NeverWakeWordDetector(),
            transcription,
            agent,
            speech,
            _promptComposer,
            TimeSpan.FromSeconds(20));
    }

    private void StartWakeLoop()
    {
        lock (_operationSync)
        {
            if (!_listening || _wakeDetector is null || _wakeLoop is { IsCompleted: false })
            {
                return;
            }

            _wakeLoopCancellation?.Dispose();
            _wakeLoopCancellation = new CancellationTokenSource();
            _wakeLoop = WakeLoopAsync(_wakeLoopCancellation.Token);
        }
    }

    private async Task PauseWakeLoopAsync()
    {
        Task? loop;
        lock (_operationSync)
        {
            _wakeLoopCancellation?.Cancel();
            loop = _wakeLoop;
            _wakeLoop = null;
        }

        if (loop is null)
        {
            return;
        }

        try
        {
            await loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Switching modes owns cancellation.
        }
    }

    private async Task WakeLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listening && _wakeDetector is not null)
        {
            try
            {
                var detected = await _wakeDetector
                    .WaitForWakeWordAsync(_frames.ReadAllAsync(cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
                if (!detected)
                {
                    return;
                }

                PublishState(AssistantState.WakeDetected);
                System.Media.SystemSounds.Asterisk.Play();
                await RunTurnAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                PublishState(AssistantState.Error);
                PublishStatus("The local wake listener stopped. Restart listening to try again.");
                return;
            }
        }
    }

    private async Task RunTurnAsync(CancellationToken cancellationToken)
    {
        await _interactionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await _apiKeyProvider.IsConfiguredAsync(cancellationToken).ConfigureAwait(false))
            {
                PublishState(AssistantState.Waiting);
                PublishStatus("Add an xAI API key before sending a spoken request. Microphone testing remains local.");
                return;
            }

            PublishState(AssistantState.Listening);
            PublishStatus("Listening for one request; speech ends after a short silence.");
            var segmenter = new UtteranceSegmenter(
                options: UtteranceSegmentationOptions.Default with
                {
                    MaximumUtterance = TimeSpan.FromSeconds(10)
                });
            var capture = await segmenter
                .CaptureAsync(_frames.ReadAllAsync(cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            if (capture is null)
            {
                PublishState(AssistantState.Waiting);
                PublishStatus("No speech was detected. Try push-to-talk again.");
                return;
            }

            var coordinator = _coordinator ?? throw new InvalidOperationException("The conversation engine is not initialized.");
            var personality = _personality ?? throw new InvalidOperationException("The personality preset is not loaded.");
            var result = await coordinator.RunTurnAsync(
                    SingleFrame(capture.Audio, cancellationToken),
                    personality.Profile,
                    voice: "eve",
                    sessionContext: personality.BuildResponseContext(new PersonalityExecutionContext()),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            PublishStatus(result is null
                ? "The request did not contain recognizable speech."
                : "Request completed; no conversation audio or transcript was stored locally.");
            PublishState(_listening ? AssistantState.Waiting : AssistantState.Sleeping);
        }
        catch (OperationCanceledException)
        {
            PublishState(_listening ? AssistantState.Waiting : AssistantState.Sleeping);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
        catch (Exception exception)
        {
            PublishState(AssistantState.Error);
            PublishStatus(ToUserMessage(exception));
        }
        finally
        {
            _interactionGate.Release();
        }
    }

    private void OnFrameAvailable(object? sender, AudioFrameEventArgs args)
    {
        try
        {
            var format = WaveFormatAdapter.ToRawAudioFormat(args.Format);
            var frame = WasapiAudioNormalizer.Normalize(args.Buffer.Span, format);
            _frames.TryWrite(frame);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or OverflowException)
        {
            if (_reportedAudioFormatError)
            {
                return;
            }

            _reportedAudioFormatError = true;
            PublishState(AssistantState.Error);
            PublishStatus("The selected microphone format cannot be converted to sixteen-kilohertz mono audio.");
        }
    }

    private void OnCoreStateChanged(object? sender, AssistantStateChangedEventArgs args) =>
        PublishState(args.Current switch
        {
            CoreAssistantState.Sleeping =>
                _listening ? AssistantState.Waiting : AssistantState.Sleeping,
            CoreAssistantState.WakeDetected => AssistantState.WakeDetected,
            CoreAssistantState.Listening => AssistantState.Listening,
            CoreAssistantState.Transcribing => AssistantState.Transcribing,
            CoreAssistantState.Thinking => AssistantState.Thinking,
            CoreAssistantState.Speaking => AssistantState.Speaking,
            CoreAssistantState.Muted => AssistantState.Muted,
            _ => AssistantState.Error
        });

    private void SetReadiness(bool hasKey, bool hasModel, string summary)
    {
        Readiness = new DesktopReadiness(hasKey, hasModel, summary);
        ReadinessChanged?.Invoke(this, Readiness);
    }

    private static string BuildReadinessSummary(bool hasKey, bool hasModel) => (hasKey, hasModel) switch
    {
        (true, true) => "Grok voice and local “Hey Computer” detection are configured.",
        (true, false) => "Grok voice is configured; wake model setup remains required.",
        (false, true) => "Local wake detection is configured; an xAI API key is required for replies.",
        _ => "Add an xAI API key and a validated wake model; microphone test mode is available now."
    };

    private static string? FindWakeModelManifest()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "models", "hey-computer", "manifest.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HeyComputer", "models", "hey-computer", "manifest.json")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string ToUserMessage(Exception exception) => exception switch
    {
        InvalidOperationException when exception.Message.Contains("API key", StringComparison.OrdinalIgnoreCase) =>
            "The xAI API key is missing or unavailable.",
        HttpRequestException => "The AI provider could not be reached. Check the network and try again.",
        TimeoutException => "The AI provider took too long to respond.",
        _ => "The spoken request could not be completed. No raw provider error was saved."
    };

    private void PublishState(AssistantState state) => StateChanged?.Invoke(this, state);

    private void PublishStatus(string status) => StatusChanged?.Invoke(this, status);

    private void CancelManualTurn()
    {
        lock (_operationSync)
        {
            _activeManualTurn?.Cancel();
        }
    }

    private async Task WaitForInteractionAsync()
    {
        await _interactionGate.WaitAsync().ConfigureAwait(false);
        _interactionGate.Release();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static async IAsyncEnumerable<AudioFrame> SingleFrame(
        AudioFrame frame,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return frame;
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listening = false;
        CancelManualTurn();
        await PauseWakeLoopAsync().ConfigureAwait(false);
        _capture.FrameAvailable -= OnFrameAvailable;
        _coordinator?.CancelActiveTurn();
        await WaitForInteractionAsync().ConfigureAwait(false);
        if (_coordinator is not null)
        {
            _coordinator.StateMachine.StateChanged -= OnCoreStateChanged;
        }

        _wakeDetector?.Dispose();
        _playback.Dispose();
        _httpClient.Dispose();
        _interactionGate.Dispose();
        _wakeLoopCancellation?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class NeverWakeWordDetector : IWakeWordDetector
    {
        public string Id => "wake-word-not-configured";

        public ProviderCapabilities Capabilities { get; } = new(true, true, true, [16_000]);

        public async Task<bool> WaitForWakeWordAsync(
            IAsyncEnumerable<AudioFrame> audio,
            CancellationToken cancellationToken = default)
        {
            await foreach (var _ in audio.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return false;
        }
    }
}
