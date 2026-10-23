namespace HeyComputer.Desktop;

public sealed class AssistantStateCoordinator
{
    private readonly object _gate = new();
    private AssistantState _state = AssistantState.Sleeping;

    public event EventHandler<AssistantState>? StateChanged;

    public AssistantState Current
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public void Set(AssistantState state)
    {
        lock (_gate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }
}
