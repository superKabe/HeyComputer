using System.Runtime.CompilerServices;
using System.Text;
using HeyComputer.Core.Audio;
using HeyComputer.Core.Personality;
using HeyComputer.Core.Providers;

namespace HeyComputer.Core.Conversation;

public sealed class ConversationCoordinator(
    IWakeWordDetector wakeWordDetector,
    ITranscriptionProvider transcriptionProvider,
    IAgentProvider agentProvider,
    ITextToSpeechProvider speechProvider,
    PersonalityPromptComposer promptComposer,
    TimeSpan? maximumUtteranceDuration = null)
{
    private readonly SemaphoreSlim _turnLock = new(1, 1);
    private readonly SemaphoreSlim _wakeLock = new(1, 1);
    private readonly object _operationSync = new();
    private readonly TimeSpan _maximumUtteranceDuration = ValidateMaximumUtteranceDuration(maximumUtteranceDuration);
    private CancellationTokenSource? _activeTurn;
    private CancellationTokenSource? _activeWakeWait;

    public AssistantStateMachine StateMachine { get; } = new();

    public async Task<bool> WaitForWakeWordAsync(
        IAsyncEnumerable<AudioFrame> audio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        await _wakeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_operationSync)
        {
            if (StateMachine.Current == AssistantState.Muted)
            {
                _wakeLock.Release();
                return false;
            }

            if (StateMachine.Current != AssistantState.Sleeping)
            {
                _wakeLock.Release();
                throw new InvalidOperationException("Wake-word detection can only begin while sleeping.");
            }

            _activeWakeWait = linked;
        }

        try
        {
            var detected = await wakeWordDetector.WaitForWakeWordAsync(audio, linked.Token).ConfigureAwait(false);
            if (!detected)
            {
                return false;
            }

            lock (_operationSync)
            {
                linked.Token.ThrowIfCancellationRequested();
                return StateMachine.TryTransitionTo(AssistantState.WakeDetected);
            }
        }
        finally
        {
            lock (_operationSync)
            {
                if (ReferenceEquals(_activeWakeWait, linked))
                {
                    _activeWakeWait = null;
                }
            }

            _wakeLock.Release();
        }
    }

    public async Task<ConversationResult?> RunTurnAsync(
        IAsyncEnumerable<AudioFrame> utterance,
        PersonalityProfile personality,
        IReadOnlyList<ConversationMessage>? history = null,
        string? voice = null,
        string? sessionContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utterance);
        ArgumentNullException.ThrowIfNull(personality);
        await _turnLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var started = DateTimeOffset.UtcNow;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            lock (_operationSync)
            {
                if (StateMachine.Current == AssistantState.Sleeping)
                {
                    StateMachine.TransitionTo(AssistantState.WakeDetected);
                }

                if (StateMachine.Current != AssistantState.WakeDetected)
                {
                    throw new InvalidOperationException($"Cannot start a turn from {StateMachine.Current}.");
                }

                _activeTurn = linked;
                StateMachine.TransitionTo(AssistantState.Listening);
                StateMachine.TransitionTo(AssistantState.Transcribing);
            }

            using var utteranceLimit = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            utteranceLimit.CancelAfter(_maximumUtteranceDuration);
            var transcript = await transcriptionProvider
                .TranscribeAsync(utterance, utteranceLimit.Token)
                .ConfigureAwait(false);

            if (transcript.IsEmpty)
            {
                ReturnToIdle();
                return null;
            }

            TransitionTo(AssistantState.Thinking, linked.Token);
            var request = new AgentRequest(
                transcript.Text.Trim(),
                personality,
                history ?? [],
                promptComposer.Compose(personality, sessionContext));

            var response = new StringBuilder();
            var chunks = CaptureResponse(
                agentProvider.StreamResponseAsync(request, linked.Token),
                response,
                linked.Token);

            TransitionTo(AssistantState.Speaking, linked.Token);
            await speechProvider.SpeakAsync(chunks, voice, linked.Token).ConfigureAwait(false);
            TransitionTo(AssistantState.Sleeping, linked.Token);

            return new ConversationResult(
                transcript.Text.Trim(),
                response.ToString(),
                DateTimeOffset.UtcNow - started);
        }
        catch (OperationCanceledException)
        {
            ReturnToIdle();
            throw;
        }
        catch
        {
            ReturnToIdle();
            throw;
        }
        finally
        {
            lock (_operationSync)
            {
                if (ReferenceEquals(_activeTurn, linked))
                {
                    _activeTurn = null;
                }
            }

            _turnLock.Release();
        }
    }

    public void CancelActiveTurn()
    {
        lock (_operationSync)
        {
            _activeTurn?.Cancel();
        }
    }

    public void Mute()
    {
        lock (_operationSync)
        {
            _activeWakeWait?.Cancel();
            _activeTurn?.Cancel();
            StateMachine.TryTransitionTo(AssistantState.Muted);
        }
    }

    public void Unmute()
    {
        lock (_operationSync)
        {
            StateMachine.TryTransitionTo(AssistantState.Sleeping);
        }
    }

    private void TransitionTo(AssistantState next, CancellationToken cancellationToken)
    {
        lock (_operationSync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StateMachine.TransitionTo(next);
        }
    }

    private void ReturnToIdle()
    {
        lock (_operationSync)
        {
            StateMachine.ReturnToIdle();
        }
    }

    private static TimeSpan ValidateMaximumUtteranceDuration(TimeSpan? value)
    {
        var duration = value ?? TimeSpan.FromSeconds(30);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Maximum utterance duration must be between zero and two minutes.");
        }

        return duration;
    }

    private static async IAsyncEnumerable<string> CaptureResponse(
        IAsyncEnumerable<string> source,
        StringBuilder response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chunk in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            response.Append(chunk);
            yield return chunk;
        }
    }
}
