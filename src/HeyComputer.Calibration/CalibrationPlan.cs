namespace HeyComputer.Calibration;

public sealed record CalibrationPlan(string WakePhrase, IReadOnlyList<CalibrationTrial> Trials)
{
    public static CalibrationPlan CreateDefault(string wakePhrase = "Hey Computer")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wakePhrase);
        var phrase = wakePhrase.Trim();

        return new CalibrationPlan(
            phrase,
            [
                new("positive-near-quiet-1", $"From nearby, say: {phrase}", true, CalibrationDistance.Near, CalibrationNoise.Quiet),
                new("positive-near-quiet-2", $"Again from nearby, say: {phrase}", true, CalibrationDistance.Near, CalibrationNoise.Quiet),
                new("positive-room-quiet-1", $"From across the room, say: {phrase}", true, CalibrationDistance.AcrossRoom, CalibrationNoise.Quiet),
                new("positive-room-quiet-2", $"Again from across the room, say: {phrase}", true, CalibrationDistance.AcrossRoom, CalibrationNoise.Quiet),
                new("positive-far-quiet", $"From your farthest normal position, say: {phrase}", true, CalibrationDistance.Far, CalibrationNoise.Quiet),
                new("positive-near-noise", $"With normal room noise, say: {phrase}", true, CalibrationDistance.Near, CalibrationNoise.Household),
                new("positive-room-media", $"With television or music playing, say: {phrase}", true, CalibrationDistance.AcrossRoom, CalibrationNoise.MediaPlaying),
                new("negative-quiet", "Speak normally without saying the wake phrase.", false, CalibrationDistance.Near, CalibrationNoise.Quiet),
                new("negative-household", "Make ordinary room sounds without saying the wake phrase.", false, CalibrationDistance.AcrossRoom, CalibrationNoise.Household),
                new("negative-media", "Let television or music play without saying the wake phrase.", false, CalibrationDistance.AcrossRoom, CalibrationNoise.MediaPlaying)
            ]);
    }

    public CalibrationPlan Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(WakePhrase);
        if (Trials.Count == 0)
        {
            throw new ArgumentException("A calibration plan must contain at least one trial.", nameof(Trials));
        }

        if (Trials.Select(trial => trial.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Trials.Count)
        {
            throw new ArgumentException("Calibration trial identifiers must be unique.", nameof(Trials));
        }

        return this;
    }
}
