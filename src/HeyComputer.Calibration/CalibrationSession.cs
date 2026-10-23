using HeyComputer.Core.Audio;

namespace HeyComputer.Calibration;

public sealed class CalibrationSession
{
    private readonly CalibrationPlan _plan;
    private readonly CalibrationThresholds _thresholds;
    private readonly ICalibrationTrialEvaluator _evaluator;
    private readonly ICalibrationStateStore _stateStore;
    private readonly List<CalibrationTrialResult> _results = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CalibrationSession(
        CalibrationPlan plan,
        CalibrationThresholds thresholds,
        ICalibrationTrialEvaluator evaluator,
        ICalibrationStateStore stateStore)
    {
        _plan = (plan ?? throw new ArgumentNullException(nameof(plan))).Validate();
        _thresholds = (thresholds ?? throw new ArgumentNullException(nameof(thresholds))).Validate();
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IReadOnlyList<CalibrationTrialResult> Results => _results.AsReadOnly();

    public CalibrationTrial? NextTrial => _plan.Trials.FirstOrDefault(
        trial => _results.All(result => !string.Equals(result.TrialId, trial.Id, StringComparison.OrdinalIgnoreCase)));

    public async Task AcceptConsentAsync(CancellationToken cancellationToken = default)
    {
        var current = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _stateStore.SaveAsync(current with { ConsentAccepted = true }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CalibrationTrialResult> RunNextTrialAsync(
        IAsyncEnumerable<AudioFrame> audio,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!state.ConsentAccepted)
            {
                throw new InvalidOperationException("Microphone calibration requires explicit consent.");
            }

            var trial = NextTrial ?? throw new InvalidOperationException("All calibration trials are complete.");
            var evaluation = await _evaluator.EvaluateAsync(audio, timeout, cancellationToken).ConfigureAwait(false);
            var result = new CalibrationTrialResult(
                trial.Id,
                trial.ExpectWakePhrase,
                evaluation.Detected,
                evaluation.Elapsed);
            _results.Add(result);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CalibrationReceipt> CompleteAsync(
        string microphoneId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(microphoneId);
        if (NextTrial is not null)
        {
            throw new InvalidOperationException("All calibration trials must be completed first.");
        }

        var assessment = CalibrationMetricsCalculator.Assess(_results, _thresholds);
        var receipt = new CalibrationReceipt(
            1,
            microphoneId.Trim(),
            completedAt,
            assessment.Passed,
            assessment.Metrics,
            _plan.WakePhrase);
        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        await _stateStore.SaveAsync(state with { LatestCalibration = receipt }, cancellationToken).ConfigureAwait(false);
        return receipt;
    }

    public async Task ResetForRerunAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _results.Clear();
            var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            await _stateStore.SaveAsync(state with { LatestCalibration = null }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
