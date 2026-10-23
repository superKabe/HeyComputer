namespace HeyComputer.ComputerControl;

public interface IComputerControlCapability
{
    ValueTask<ComputerControlResult> ExecuteAsync(
        ComputerControlCommand command,
        ComputerControlRequestContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ComputerControlRequestContext(
    string CorrelationId,
    string RequestedBy = "conversation-agent")
{
    public static ComputerControlRequestContext Create(string? correlationId = null) =>
        new(correlationId ?? Guid.NewGuid().ToString("N"));
}

public abstract record ComputerControlCommand
{
    private ComputerControlCommand() { }

    public sealed record ListRunningApplications : ComputerControlCommand;

    public sealed record CheckProcessRunning(string ProcessName) : ComputerControlCommand;

    public sealed record LaunchAllowlistedTarget(string TargetId) : ComputerControlCommand;

    public sealed record ToggleMediaPlayback : ComputerControlCommand;
}

public enum ComputerControlStatus
{
    Succeeded,
    Rejected,
    Failed
}

public sealed record ComputerControlResult(
    ComputerControlStatus Status,
    string Summary,
    IReadOnlyList<ComputerControlEvidence> Evidence,
    ComputerControlAuditMetadata Audit)
{
    public bool Succeeded => Status == ComputerControlStatus.Succeeded;
}

public sealed record ComputerControlAuditMetadata(
    string OperationId,
    string CorrelationId,
    string RequestedBy,
    string Capability,
    string Action,
    string? Target,
    bool IsReadOnly,
    bool IsReversible,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public abstract record ComputerControlEvidence(string Kind)
{
    public sealed record RunningApplications(IReadOnlyList<RunningApplication> Applications)
        : ComputerControlEvidence("running-applications");

    public sealed record ProcessState(string ProcessName, bool IsRunning, IReadOnlyList<int> ProcessIds)
        : ComputerControlEvidence("process-state");

    public sealed record LaunchState(string TargetId, string DisplayName)
        : ComputerControlEvidence("launch-state");

    public sealed record MediaKeyState(string Key)
        : ComputerControlEvidence("media-key-state");

    public sealed record ErrorState(string Code, string Message)
        : ComputerControlEvidence("error");
}

public sealed record RunningApplication(int ProcessId, string ProcessName, string? WindowTitle);
