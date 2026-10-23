namespace HeyComputer.Desktop;

public enum AssistantState
{
    Sleeping,
    Waiting,
    WakeDetected,
    Listening,
    Transcribing,
    Thinking,
    Speaking,
    Muted,
    Error
}

public static class AssistantStateExtensions
{
    public static string ToDisplayText(this AssistantState state) => state switch
    {
        AssistantState.Sleeping => "Stopped",
        AssistantState.Waiting => "Waiting locally for “Hey Computer”",
        AssistantState.WakeDetected => "Wake phrase detected",
        AssistantState.Listening => "Listening",
        AssistantState.Transcribing => "Transcribing",
        AssistantState.Thinking => "Thinking",
        AssistantState.Speaking => "Speaking",
        AssistantState.Muted => "Microphone muted",
        AssistantState.Error => "Needs attention",
        _ => state.ToString()
    };
}
