namespace HeyComputer.Core.Conversation;

public sealed class AssistantStateChangedEventArgs(
    AssistantState previous,
    AssistantState current,
    DateTimeOffset occurredAt) : EventArgs
{
    public AssistantState Previous { get; } = previous;

    public AssistantState Current { get; } = current;

    public DateTimeOffset OccurredAt { get; } = occurredAt;
}
