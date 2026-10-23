using HeyComputer.Core.Conversation;

namespace HeyComputer.Core.Tests;

public sealed class AssistantStateMachineTests
{
    [Fact]
    public void NormalTurn_AllowsEveryExpectedTransition()
    {
        var machine = new AssistantStateMachine();
        var observed = new List<AssistantState>();
        machine.StateChanged += (_, args) => observed.Add(args.Current);

        machine.TransitionTo(AssistantState.WakeDetected);
        machine.TransitionTo(AssistantState.Listening);
        machine.TransitionTo(AssistantState.Transcribing);
        machine.TransitionTo(AssistantState.Thinking);
        machine.TransitionTo(AssistantState.Speaking);
        machine.TransitionTo(AssistantState.Sleeping);

        Assert.Equal(
            [
                AssistantState.WakeDetected,
                AssistantState.Listening,
                AssistantState.Transcribing,
                AssistantState.Thinking,
                AssistantState.Speaking,
                AssistantState.Sleeping
            ],
            observed);
        Assert.Equal(AssistantState.Sleeping, machine.Current);
    }

    [Fact]
    public void InvalidTransition_IsRejectedWithoutChangingState()
    {
        var machine = new AssistantStateMachine();

        var exception = Assert.Throws<InvalidOperationException>(
            () => machine.TransitionTo(AssistantState.Speaking));

        Assert.Contains("Sleeping -> Speaking", exception.Message);
        Assert.Equal(AssistantState.Sleeping, machine.Current);
    }

    [Fact]
    public void MutingAndUnmuting_ReturnsToSleeping()
    {
        var machine = new AssistantStateMachine();

        machine.TransitionTo(AssistantState.Muted);
        machine.TransitionTo(AssistantState.Sleeping);

        Assert.Equal(AssistantState.Sleeping, machine.Current);
    }

    [Fact]
    public void ReturnToIdle_DoesNotOverrideConcurrentMute()
    {
        var machine = new AssistantStateMachine();
        machine.TransitionTo(AssistantState.WakeDetected);
        machine.TransitionTo(AssistantState.Muted);

        machine.ReturnToIdle();

        Assert.Equal(AssistantState.Muted, machine.Current);
    }

    [Fact]
    public void TryTransition_InvalidTransitionReturnsFalseWithoutMutation()
    {
        var machine = new AssistantStateMachine();

        var changed = machine.TryTransitionTo(AssistantState.Thinking);

        Assert.False(changed);
        Assert.Equal(AssistantState.Sleeping, machine.Current);
    }
}
