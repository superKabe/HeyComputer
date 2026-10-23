namespace HeyComputer.Core.Conversation;

public sealed class AssistantStateMachine
{
    private static readonly IReadOnlyDictionary<AssistantState, HashSet<AssistantState>> AllowedTransitions =
        new Dictionary<AssistantState, HashSet<AssistantState>>
        {
            [AssistantState.Sleeping] = [AssistantState.WakeDetected, AssistantState.Muted],
            [AssistantState.WakeDetected] = [AssistantState.Listening, AssistantState.Sleeping, AssistantState.Muted],
            [AssistantState.Listening] = [AssistantState.Transcribing, AssistantState.Sleeping, AssistantState.Muted],
            [AssistantState.Transcribing] = [AssistantState.Thinking, AssistantState.Sleeping, AssistantState.Muted],
            [AssistantState.Thinking] = [AssistantState.Speaking, AssistantState.Sleeping, AssistantState.Muted],
            [AssistantState.Speaking] = [AssistantState.Sleeping, AssistantState.Muted],
            [AssistantState.Muted] = [AssistantState.Sleeping]
        };

    private readonly object _sync = new();
    private AssistantState _current = AssistantState.Sleeping;

    public event EventHandler<AssistantStateChangedEventArgs>? StateChanged;

    public AssistantState Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void TransitionTo(AssistantState next)
    {
        if (!TryTransitionTo(next, out var previous))
        {
            throw new InvalidOperationException($"Invalid assistant state transition: {previous} -> {next}.");
        }
    }

    public bool TryTransitionTo(AssistantState next) => TryTransitionTo(next, out _);

    public bool TryTransitionTo(AssistantState next, out AssistantState previous)
    {
        AssistantStateChangedEventArgs? change = null;

        lock (_sync)
        {
            previous = _current;
            if (previous == next)
            {
                return true;
            }

            if (!AllowedTransitions[previous].Contains(next))
            {
                return false;
            }

            _current = next;
            change = new AssistantStateChangedEventArgs(previous, next, DateTimeOffset.UtcNow);
        }

        StateChanged?.Invoke(this, change);
        return true;
    }

    public void ReturnToIdle()
    {
        AssistantStateChangedEventArgs? change = null;

        lock (_sync)
        {
            if (_current is AssistantState.Sleeping or AssistantState.Muted)
            {
                return;
            }

            var previous = _current;
            _current = AssistantState.Sleeping;
            change = new AssistantStateChangedEventArgs(previous, AssistantState.Sleeping, DateTimeOffset.UtcNow);
        }

        StateChanged?.Invoke(this, change);
    }
}
