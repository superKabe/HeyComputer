using HeyComputer.Core.Audio;
using HeyComputer.Core.Providers;

namespace HeyComputer.Calibration;

public interface ICalibrationStateStore
{
    Task<CalibrationProfileState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CalibrationProfileState state, CancellationToken cancellationToken = default);
}

public interface ICalibrationTrialEvaluator
{
    Task<(bool Detected, TimeSpan Elapsed)> EvaluateAsync(
        IAsyncEnumerable<AudioFrame> audio,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public sealed class WakeWordTrialEvaluator(IWakeWordDetector detector) : ICalibrationTrialEvaluator
{
    public async Task<(bool Detected, TimeSpan Elapsed)> EvaluateAsync(
        IAsyncEnumerable<AudioFrame> audio,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var started = TimeProvider.System.GetTimestamp();
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            var detected = await detector.WaitForWakeWordAsync(audio, linked.Token).ConfigureAwait(false);
            return (detected, TimeProvider.System.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return (false, TimeProvider.System.GetElapsedTime(started));
        }
    }
}

public sealed class InMemoryCalibrationStateStore : ICalibrationStateStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CalibrationProfileState _state = CalibrationProfileState.Empty;

    public async Task<CalibrationProfileState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _state;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CalibrationProfileState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _state = state;
        }
        finally
        {
            _gate.Release();
        }
    }
}
