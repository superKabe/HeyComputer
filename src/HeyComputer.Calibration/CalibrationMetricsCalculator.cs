namespace HeyComputer.Calibration;

public static class CalibrationMetricsCalculator
{
    public static CalibrationMetrics Calculate(IReadOnlyCollection<CalibrationTrialResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var positives = results.Where(result => result.ExpectedWakePhrase).ToArray();
        var negatives = results.Where(result => !result.ExpectedWakePhrase).ToArray();
        var detections = positives.Where(result => result.Detected).Select(result => result.Elapsed).Order().ToArray();
        var truePositives = detections.Length;
        var falseNegatives = positives.Length - truePositives;
        var falsePositives = negatives.Count(result => result.Detected);
        var trueNegatives = negatives.Length - falsePositives;

        return new CalibrationMetrics(
            positives.Length,
            negatives.Length,
            truePositives,
            falseNegatives,
            falsePositives,
            trueNegatives,
            positives.Length == 0 ? 0 : (double)truePositives / positives.Length,
            negatives.Length == 0 ? 0 : (double)falsePositives / negatives.Length,
            detections.Length == 0 ? null : TimeSpan.FromTicks((long)detections.Average(value => value.Ticks)),
            detections.Length == 0 ? null : detections[(int)Math.Ceiling(detections.Length * 0.95) - 1]);
    }

    public static CalibrationAssessment Assess(
        IReadOnlyCollection<CalibrationTrialResult> results,
        CalibrationThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholds.Validate();
        var metrics = Calculate(results);
        var reasons = new List<string>();

        if (metrics.PositiveTrials < thresholds.MinimumPositiveTrials)
        {
            reasons.Add($"At least {thresholds.MinimumPositiveTrials} wake-phrase trials are required.");
        }

        if (metrics.NegativeTrials < thresholds.MinimumNegativeTrials)
        {
            reasons.Add($"At least {thresholds.MinimumNegativeTrials} negative-control trials are required.");
        }

        if (metrics.Recall < thresholds.MinimumRecall)
        {
            reasons.Add("Wake-phrase recall is below the provisional acceptance threshold.");
        }

        if (metrics.FalseActivationRate > thresholds.MaximumFalseActivationRate)
        {
            reasons.Add("False activation rate is above the provisional acceptance threshold.");
        }

        if (thresholds.MaximumP95DetectionLatency is { } maximumLatency &&
            (metrics.P95DetectionLatency is null || metrics.P95DetectionLatency > maximumLatency))
        {
            reasons.Add("Wake detection latency is above the provisional acceptance threshold.");
        }

        return new CalibrationAssessment(metrics, reasons.Count == 0, reasons);
    }
}
