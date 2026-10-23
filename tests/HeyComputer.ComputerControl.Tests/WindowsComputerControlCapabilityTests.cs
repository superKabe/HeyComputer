namespace HeyComputer.ComputerControl.Tests;

public sealed class WindowsComputerControlCapabilityTests
{
    [Fact]
    public async Task ListsApplicationsInDeterministicOrderWithAuditEvidence()
    {
        var processes = new FakeProcessCatalog(
        [
            new(22, "spotify", "Music"),
            new(30, "Discord", null),
            new(10, "discord", "Friends")
        ]);
        var capability = CreateCapability(processes: processes);

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.ListRunningApplications(),
            new ComputerControlRequestContext("turn-1", "orchestrator"));

        Assert.True(result.Succeeded);
        var evidence = Assert.IsType<ComputerControlEvidence.RunningApplications>(Assert.Single(result.Evidence));
        Assert.Equal([10, 30, 22], evidence.Applications.Select(application => application.ProcessId));
        Assert.Equal("process.list", result.Audit.Action);
        Assert.True(result.Audit.IsReadOnly);
        Assert.Equal("turn-1", result.Audit.CorrelationId);
        Assert.Equal("orchestrator", result.Audit.RequestedBy);
        Assert.True(result.Audit.CompletedAtUtc >= result.Audit.StartedAtUtc);
    }

    [Theory]
    [InlineData("spotify")]
    [InlineData("SPOTIFY.EXE")]
    [InlineData(" spotify.exe ")]
    public async Task ChecksNamedProcessWithoutDependingOnExeSuffix(string requestedName)
    {
        var capability = CreateCapability(processes: new FakeProcessCatalog([new(7, "Spotify", null)]));

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.CheckProcessRunning(requestedName),
            ComputerControlRequestContext.Create("turn-2"));

        var evidence = Assert.IsType<ComputerControlEvidence.ProcessState>(Assert.Single(result.Evidence));
        Assert.True(evidence.IsRunning);
        Assert.Equal([7], evidence.ProcessIds);
        Assert.Equal("spotify", evidence.ProcessName, ignoreCase: true);
    }

    [Fact]
    public async Task ReportsNamedProcessIsNotRunning()
    {
        var capability = CreateCapability(processes: new FakeProcessCatalog([]));

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.CheckProcessRunning("RocketLeague.exe"),
            ComputerControlRequestContext.Create());

        var evidence = Assert.IsType<ComputerControlEvidence.ProcessState>(Assert.Single(result.Evidence));
        Assert.False(evidence.IsRunning);
        Assert.Empty(evidence.ProcessIds);
    }

    [Fact]
    public async Task RejectsEmptyProcessNameWithoutObservingProcesses()
    {
        var processes = new FakeProcessCatalog([]);
        var capability = CreateCapability(processes: processes);

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.CheckProcessRunning("  "),
            ComputerControlRequestContext.Create());

        Assert.Equal(ComputerControlStatus.Rejected, result.Status);
        Assert.Equal(0, processes.CallCount);
    }

    [Fact]
    public async Task LaunchesOnlyTheConfiguredAllowlistEntry()
    {
        var launcher = new FakeLauncher();
        var target = new LaunchTarget("spotify", "Spotify", LaunchTargetKind.Uri, "spotify:");
        var capability = CreateCapability(launcher: launcher, targets: [target]);

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.LaunchAllowlistedTarget("SPOTIFY"),
            ComputerControlRequestContext.Create());

        Assert.True(result.Succeeded);
        Assert.Equal(target, Assert.Single(launcher.Launched));
        Assert.Equal("target.launch", result.Audit.Action);
        Assert.Equal("spotify", result.Audit.Target);
        Assert.False(result.Audit.IsReadOnly);
        Assert.False(result.Audit.IsReversible);
    }

    [Fact]
    public async Task RejectsUnknownLaunchTargetWithoutStartingAnything()
    {
        var launcher = new FakeLauncher();
        var capability = CreateCapability(launcher: launcher);

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.LaunchAllowlistedTarget("powershell"),
            ComputerControlRequestContext.Create());

        Assert.Equal(ComputerControlStatus.Rejected, result.Status);
        Assert.Empty(launcher.Launched);
        Assert.IsType<ComputerControlEvidence.ErrorState>(Assert.Single(result.Evidence));
    }

    [Fact]
    public async Task SendsExactlyOnePlayPauseToggle()
    {
        var media = new FakeMediaKeySender();
        var capability = CreateCapability(media: media);

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.ToggleMediaPlayback(),
            ComputerControlRequestContext.Create());

        Assert.True(result.Succeeded);
        Assert.Equal(1, media.ToggleCount);
        Assert.True(result.Audit.IsReversible);
        Assert.Equal("media.play-pause", result.Audit.Action);
    }

    [Fact]
    public async Task HonorsCancellationBeforeAnySideEffect()
    {
        var launcher = new FakeLauncher();
        var capability = CreateCapability(
            launcher: launcher,
            targets: [new("spotify", "Spotify", LaunchTargetKind.Uri, "spotify:")]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await capability.ExecuteAsync(
                new ComputerControlCommand.LaunchAllowlistedTarget("spotify"),
                ComputerControlRequestContext.Create(),
                cancellation.Token));
        Assert.Empty(launcher.Launched);
    }

    [Fact]
    public async Task ConvertsExpectedWindowsFailureIntoAuditableFailure()
    {
        var media = new FakeMediaKeySender
        {
            Exception = new PlatformNotSupportedException("Not Windows")
        };
        var capability = CreateCapability(media: media);

        var result = await capability.ExecuteAsync(
            new ComputerControlCommand.ToggleMediaPlayback(),
            ComputerControlRequestContext.Create());

        Assert.Equal(ComputerControlStatus.Failed, result.Status);
        var error = Assert.IsType<ComputerControlEvidence.ErrorState>(Assert.Single(result.Evidence));
        Assert.Equal(nameof(PlatformNotSupportedException), error.Code);
    }

    private static WindowsComputerControlCapability CreateCapability(
        FakeProcessCatalog? processes = null,
        FakeLauncher? launcher = null,
        FakeMediaKeySender? media = null,
        IReadOnlyList<LaunchTarget>? targets = null) =>
        new(
            new LaunchAllowlist(targets ?? []),
            processes ?? new FakeProcessCatalog([]),
            launcher ?? new FakeLauncher(),
            media ?? new FakeMediaKeySender(),
            new FakeClock());

    private sealed class FakeProcessCatalog(IReadOnlyList<RunningApplication> applications) : IProcessCatalog
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<RunningApplication> GetRunningApplications()
        {
            CallCount++;
            return applications;
        }
    }

    private sealed class FakeLauncher : IAllowlistedTargetLauncher
    {
        public List<LaunchTarget> Launched { get; } = [];

        public void Launch(LaunchTarget target) => Launched.Add(target);
    }

    private sealed class FakeMediaKeySender : IMediaKeySender
    {
        public Exception? Exception { get; init; }
        public int ToggleCount { get; private set; }

        public void TogglePlayPause()
        {
            ToggleCount++;
            if (Exception is not null)
            {
                throw Exception;
            }
        }
    }

    private sealed class FakeClock : IComputerControlClock
    {
        private DateTimeOffset _now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow
        {
            get
            {
                var value = _now;
                _now = _now.AddMilliseconds(5);
                return value;
            }
        }
    }
}
