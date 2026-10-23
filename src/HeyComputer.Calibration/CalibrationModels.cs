namespace HeyComputer.Calibration;

public enum CalibrationDistance
{
    Near,
    AcrossRoom,
    Far
}

public enum CalibrationNoise
{
    Quiet,
    Household,
    MediaPlaying
}

public sealed record CalibrationTrial
{
    public CalibrationTrial(
        string id,
        string prompt,
        bool expectWakePhrase,
        CalibrationDistance distance,
        CalibrationNoise noise)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        Id = id;
        Prompt = prompt;
        ExpectWakePhrase = expectWakePhrase;
        Distance = distance;
        Noise = noise;
    }

    public string Id { get; }
    public string Prompt { get; }
    public bool ExpectWakePhrase { get; }
    public CalibrationDistance Distance { get; }
    public CalibrationNoise Noise { get; }
}

public sealed record CalibrationTrialResult(
    string TrialId,
    bool ExpectedWakePhrase,
    bool Detected,
    TimeSpan Elapsed)
{
    public bool IsCorrect => ExpectedWakePhrase == Detected;
}

public sealed record CalibrationMetrics(
    int PositiveTrials,
    int NegativeTrials,
    int TruePositives,
    int FalseNegatives,
    int FalsePositives,
    int TrueNegatives,
    double Recall,
    double FalseActivationRate,
    TimeSpan? AverageDetectionLatency,
    TimeSpan? P95DetectionLatency);

public sealed record CalibrationThresholds(
    int MinimumPositiveTrials = 5,
    int MinimumNegativeTrials = 3,
    double MinimumRecall = 0.90,
    double MaximumFalseActivationRate = 0.05,
    TimeSpan? MaximumP95DetectionLatency = null)
{
    public CalibrationThresholds Validate()
    {
        if (MinimumPositiveTrials < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumPositiveTrials));
        }

        if (MinimumNegativeTrials < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumNegativeTrials));
        }

        if (MinimumRecall is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumRecall));
        }

        if (MaximumFalseActivationRate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumFalseActivationRate));
        }

        if (MaximumP95DetectionLatency is { } latency && latency <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumP95DetectionLatency));
        }

        return this;
    }
}

public sealed record CalibrationAssessment(
    CalibrationMetrics Metrics,
    bool Passed,
    IReadOnlyList<string> Reasons);

public sealed record CalibrationReceipt(
    int SchemaVersion,
    string MicrophoneId,
    DateTimeOffset CompletedAt,
    bool Passed,
    CalibrationMetrics Metrics,
    string WakePhrase,
    bool AudioRetained = false);

public sealed record CalibrationProfileState(
    bool ConsentAccepted,
    CalibrationReceipt? LatestCalibration = null,
    EasterEggTriggerMetadata? EasterEggTrigger = null)
{
    public static CalibrationProfileState Empty { get; } = new(false);
}
