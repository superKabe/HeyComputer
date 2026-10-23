namespace HeyComputer.Calibration;

public sealed class CalibrationViewModel(CalibrationPlan plan)
{
    private readonly CalibrationPlan _plan = (plan ?? throw new ArgumentNullException(nameof(plan))).Validate();

    public int CompletedTrials { get; private set; }

    public int TotalTrials => _plan.Trials.Count;

    public double Progress => TotalTrials == 0 ? 0 : (double)CompletedTrials / TotalTrials;

    public CalibrationTrial? CurrentTrial => CompletedTrials < TotalTrials ? _plan.Trials[CompletedTrials] : null;

    public string Prompt => CurrentTrial?.Prompt ?? "Calibration complete.";

    public bool IsComplete => CompletedTrials == TotalTrials;

    public void RecordCompletedTrial()
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Calibration is already complete.");
        }

        CompletedTrials++;
    }

    public void Reset() => CompletedTrials = 0;
}
