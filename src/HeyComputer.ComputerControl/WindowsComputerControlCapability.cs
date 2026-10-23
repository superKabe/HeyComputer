namespace HeyComputer.ComputerControl;

public sealed class WindowsComputerControlCapability : IComputerControlCapability
{
    public const string CapabilityName = "windows.local-control";

    private readonly IProcessCatalog _processCatalog;
    private readonly LaunchAllowlist _allowlist;
    private readonly IAllowlistedTargetLauncher _launcher;
    private readonly IMediaKeySender _mediaKeySender;
    private readonly IComputerControlClock _clock;

    public WindowsComputerControlCapability(
        LaunchAllowlist allowlist,
        IProcessCatalog? processCatalog = null,
        IAllowlistedTargetLauncher? launcher = null,
        IMediaKeySender? mediaKeySender = null,
        IComputerControlClock? clock = null)
    {
        _allowlist = allowlist ?? throw new ArgumentNullException(nameof(allowlist));
        _processCatalog = processCatalog ?? new WindowsProcessCatalog();
        _launcher = launcher ?? new WindowsAllowlistedTargetLauncher();
        _mediaKeySender = mediaKeySender ?? new WindowsMediaKeySender();
        _clock = clock ?? new SystemComputerControlClock();
    }

    public ValueTask<ComputerControlResult> ExecuteAsync(
        ComputerControlCommand command,
        ComputerControlRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAt = _clock.UtcNow;

        try
        {
            var result = command switch
            {
                ComputerControlCommand.ListRunningApplications => ListApplications(context, startedAt, cancellationToken),
                ComputerControlCommand.CheckProcessRunning check => CheckProcess(check, context, startedAt, cancellationToken),
                ComputerControlCommand.LaunchAllowlistedTarget launch => Launch(launch, context, startedAt, cancellationToken),
                ComputerControlCommand.ToggleMediaPlayback => ToggleMedia(context, startedAt, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };

            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                          System.ComponentModel.Win32Exception or
                                          PlatformNotSupportedException)
        {
            return ValueTask.FromResult(CreateResult(
                ComputerControlStatus.Failed,
                "The requested computer action failed.",
                [new ComputerControlEvidence.ErrorState(exception.GetType().Name, exception.Message)],
                context,
                command,
                startedAt));
        }
    }

    private ComputerControlResult ListApplications(
        ComputerControlRequestContext context,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var applications = _processCatalog.GetRunningApplications()
            .OrderBy(application => application.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(application => application.ProcessId)
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResult(
            ComputerControlStatus.Succeeded,
            $"Found {applications.Length} running processes.",
            [new ComputerControlEvidence.RunningApplications(applications)],
            context,
            new ComputerControlCommand.ListRunningApplications(),
            startedAt);
    }

    private ComputerControlResult CheckProcess(
        ComputerControlCommand.CheckProcessRunning command,
        ComputerControlRequestContext context,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var requestedName = NormalizeProcessName(command.ProcessName);
        if (requestedName.Length == 0)
        {
            return Rejected("A process name is required.", context, command, startedAt);
        }

        var applications = _processCatalog.GetRunningApplications();
        cancellationToken.ThrowIfCancellationRequested();

        var processIds = applications
            .Where(application => string.Equals(
                NormalizeProcessName(application.ProcessName),
                requestedName,
                StringComparison.OrdinalIgnoreCase))
            .Select(application => application.ProcessId)
            .Order()
            .ToArray();

        return CreateResult(
            ComputerControlStatus.Succeeded,
            processIds.Length > 0
                ? $"{requestedName} is running."
                : $"{requestedName} is not running.",
            [new ComputerControlEvidence.ProcessState(requestedName, processIds.Length > 0, processIds)],
            context,
            command,
            startedAt);
    }

    private ComputerControlResult Launch(
        ComputerControlCommand.LaunchAllowlistedTarget command,
        ComputerControlRequestContext context,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        if (!_allowlist.TryGet(command.TargetId, out var target) || target is null)
        {
            return Rejected(
                "That launch target is not allowlisted.",
                context,
                command,
                startedAt,
                command.TargetId);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _launcher.Launch(target);

        return CreateResult(
            ComputerControlStatus.Succeeded,
            $"Launched {target.DisplayName}.",
            [new ComputerControlEvidence.LaunchState(target.Id, target.DisplayName)],
            context,
            command,
            startedAt,
            target.Id);
    }

    private ComputerControlResult ToggleMedia(
        ComputerControlRequestContext context,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _mediaKeySender.TogglePlayPause();

        return CreateResult(
            ComputerControlStatus.Succeeded,
            "Toggled media playback.",
            [new ComputerControlEvidence.MediaKeyState("play-pause")],
            context,
            new ComputerControlCommand.ToggleMediaPlayback(),
            startedAt);
    }

    private ComputerControlResult Rejected(
        string summary,
        ComputerControlRequestContext context,
        ComputerControlCommand command,
        DateTimeOffset startedAt,
        string? target = null) =>
        CreateResult(
            ComputerControlStatus.Rejected,
            summary,
            [new ComputerControlEvidence.ErrorState("not-allowed", summary)],
            context,
            command,
            startedAt,
            target);

    private ComputerControlResult CreateResult(
        ComputerControlStatus status,
        string summary,
        IReadOnlyList<ComputerControlEvidence> evidence,
        ComputerControlRequestContext context,
        ComputerControlCommand command,
        DateTimeOffset startedAt,
        string? explicitTarget = null)
    {
        var (action, target, readOnly, reversible) = Describe(command);

        return new ComputerControlResult(
            status,
            summary,
            evidence,
            new ComputerControlAuditMetadata(
                Guid.NewGuid().ToString("N"),
                context.CorrelationId,
                context.RequestedBy,
                CapabilityName,
                action,
                explicitTarget ?? target,
                readOnly,
                reversible,
                startedAt,
                _clock.UtcNow));
    }

    private static (string Action, string? Target, bool IsReadOnly, bool IsReversible) Describe(
        ComputerControlCommand command) => command switch
    {
        ComputerControlCommand.ListRunningApplications => ("process.list", null, true, true),
        ComputerControlCommand.CheckProcessRunning check =>
            ("process.check", NormalizeProcessName(check.ProcessName), true, true),
        ComputerControlCommand.LaunchAllowlistedTarget launch =>
            ("target.launch", launch.TargetId, false, false),
        ComputerControlCommand.ToggleMediaPlayback => ("media.play-pause", null, false, true),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
