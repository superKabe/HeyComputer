using System.Runtime.CompilerServices;
using HeyComputer.Core.Audio;
using HeyComputer.Core.Conversation;
using HeyComputer.Core.Personality;
using HeyComputer.Core.Providers;
using HeyComputer.Core.Testing;

namespace HeyComputer.Core.Tests;

public sealed class ConversationCoordinatorTests
{
    [Fact]
    public async Task CompleteTurn_StreamsProviderOutputToSpeechAndReturnsToSleep()
    {
        var transcription = new FakeTranscriptionProvider("turn on the lights");
        var agent = new FakeAgentProvider("Sure", ", doing that.");
        var speech = new FakeTextToSpeechProvider();
        var coordinator = CreateCoordinator(transcription, agent, speech);

        var result = await coordinator.RunTurnAsync(
            Frames(3),
            PersonalityProfile.Helpful,
            voice: "fake",
            sessionContext: "The user is testing the app.");

        Assert.NotNull(result);
        Assert.Equal("turn on the lights", result.Transcript);
        Assert.Equal("Sure, doing that.", result.ResponseText);
        Assert.Equal(result.ResponseText, speech.SpokenText);
        Assert.Equal("fake", speech.SelectedVoice);
        Assert.Equal(3, transcription.FramesReceived);
        Assert.Equal(AssistantState.Sleeping, coordinator.StateMachine.Current);
        Assert.NotNull(agent.LastRequest);
        Assert.Contains("one or two sentences", agent.LastRequest.Instructions);
        Assert.Contains("testing the app", agent.LastRequest.Instructions);
    }

    [Fact]
    public async Task EmptyTranscript_SkipsAgentAndSpeech()
    {
        var agent = new FakeAgentProvider("should not run");
        var speech = new FakeTextToSpeechProvider();
        var coordinator = CreateCoordinator(new FakeTranscriptionProvider("  "), agent, speech);

        var result = await coordinator.RunTurnAsync(Frames(1), PersonalityProfile.Helpful);

        Assert.Null(result);
        Assert.Null(agent.LastRequest);
        Assert.Empty(speech.SpokenText);
        Assert.Equal(AssistantState.Sleeping, coordinator.StateMachine.Current);
    }

    [Fact]
    public async Task ProviderFailure_ReturnsToSleepAndPreservesException()
    {
        var coordinator = new ConversationCoordinator(
            new FakeWakeWordDetector(),
            new ThrowingTranscriptionProvider(),
            new FakeAgentProvider(),
            new FakeTextToSpeechProvider(),
            new PersonalityPromptComposer());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.RunTurnAsync(Frames(1), PersonalityProfile.Helpful));

        Assert.Equal(AssistantState.Sleeping, coordinator.StateMachine.Current);
    }

    [Fact]
    public async Task WakeDetection_TransitionsOnlyWhenDetected()
    {
        var coordinator = CreateCoordinator(
            new FakeTranscriptionProvider(),
            new FakeAgentProvider(),
            new FakeTextToSpeechProvider(),
            new FakeWakeWordDetector(true));

        var detected = await coordinator.WaitForWakeWordAsync(Frames(1));

        Assert.True(detected);
        Assert.Equal(AssistantState.WakeDetected, coordinator.StateMachine.Current);
    }

    [Fact]
    public void Mute_BlocksWakeDetectionUntilUnmuted()
    {
        var coordinator = CreateCoordinator(
            new FakeTranscriptionProvider(),
            new FakeAgentProvider(),
            new FakeTextToSpeechProvider());

        coordinator.Mute();
        Assert.Equal(AssistantState.Muted, coordinator.StateMachine.Current);

        coordinator.Unmute();
        Assert.Equal(AssistantState.Sleeping, coordinator.StateMachine.Current);
    }

    [Fact]
    public async Task Mute_CancelsPendingWakeDetectionAndKeepsMutedState()
    {
        var detector = new BlockingWakeWordDetector();
        var coordinator = CreateCoordinator(
            new FakeTranscriptionProvider(),
            new FakeAgentProvider(),
            new FakeTextToSpeechProvider(),
            detector);

        var pending = coordinator.WaitForWakeWordAsync(Frames(1));
        await detector.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        coordinator.Mute();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(AssistantState.Muted, coordinator.StateMachine.Current);
    }

    [Fact]
    public async Task MaximumUtteranceDuration_CancelsStalledTranscription()
    {
        var transcription = new BlockingTranscriptionProvider();
        var coordinator = new ConversationCoordinator(
            new FakeWakeWordDetector(),
            transcription,
            new FakeAgentProvider(),
            new FakeTextToSpeechProvider(),
            new PersonalityPromptComposer(),
            TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.RunTurnAsync(Frames(1), PersonalityProfile.Helpful));

        Assert.Equal(AssistantState.Sleeping, coordinator.StateMachine.Current);
    }

    [Fact]
    public async Task CancelActiveTurn_CancelsProviderWithoutDisposedTokenRace()
    {
        var transcription = new BlockingTranscriptionProvider();
        var coordinator = CreateCoordinator(
            transcription,
            new FakeAgentProvider(),
            new FakeTextToSpeechProvider());

        var pending = coordinator.RunTurnAsync(Frames(1), PersonalityProfile.Helpful);
        await transcription.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        coordinator.CancelActiveTurn();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(AssistantState.Sleeping, coordinator.StateMachine.Current);
    }

    private static ConversationCoordinator CreateCoordinator(
        ITranscriptionProvider transcription,
        IAgentProvider agent,
        ITextToSpeechProvider speech,
        IWakeWordDetector? wakeWord = null) =>
        new(
            wakeWord ?? new FakeWakeWordDetector(),
            transcription,
            agent,
            speech,
            new PersonalityPromptComposer());

    private static async IAsyncEnumerable<AudioFrame> Frames(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new AudioFrame(new float[160]);
        }
    }

    private sealed class ThrowingTranscriptionProvider : ITranscriptionProvider
    {
        public string Id => "throwing-transcription";

        public ProviderCapabilities Capabilities { get; } = new(true, false, true);

        public Task<TranscriptionResult> TranscribeAsync(
            IAsyncEnumerable<AudioFrame> audio,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Expected test failure.");
    }

    private sealed class BlockingTranscriptionProvider : ITranscriptionProvider
    {
        public string Id => "blocking-transcription";

        public ProviderCapabilities Capabilities { get; } = new(true, true, true);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<TranscriptionResult> TranscribeAsync(
            IAsyncEnumerable<AudioFrame> audio,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new TranscriptionResult("unreachable");
        }
    }

    private sealed class BlockingWakeWordDetector : IWakeWordDetector
    {
        public string Id => "blocking-wake-word";

        public ProviderCapabilities Capabilities { get; } = new(true, true, true, [16_000]);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> WaitForWakeWordAsync(
            IAsyncEnumerable<AudioFrame> audio,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        }
    }
}
