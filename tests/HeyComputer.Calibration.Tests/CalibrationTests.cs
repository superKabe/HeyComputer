using HeyComputer.Calibration;
using HeyComputer.Core.Audio;

namespace HeyComputer.Calibration.Tests;

public sealed class CalibrationTests
{
    [Fact]
    public void DefaultPlanCoversDistanceNoiseAndNegativeControls()
    {
        var plan = CalibrationPlan.CreateDefault();

        Assert.Equal("Hey Computer", plan.WakePhrase);
        Assert.Contains(plan.Trials, trial => trial.Distance == CalibrationDistance.Far);
        Assert.Contains(plan.Trials, trial => trial.Noise == CalibrationNoise.MediaPlaying);
        Assert.True(plan.Trials.Count(trial => trial.ExpectWakePhrase) >= 5);
        Assert.True(plan.Trials.Count(trial => !trial.ExpectWakePhrase) >= 3);
    }

    [Fact]
    public void MetricsMeasureRecallFalseActivationsAndLatency()
    {
        CalibrationTrialResult[] results =
        [
            new("p1", true, true, TimeSpan.FromMilliseconds(100)),
            new("p2", true, false, TimeSpan.FromMilliseconds(200)),
            new("n1", false, true, TimeSpan.FromMilliseconds(300)),
            new("n2", false, false, TimeSpan.FromMilliseconds(400))
        ];

        var metrics = CalibrationMetricsCalculator.Calculate(results);

        Assert.Equal(0.5, metrics.Recall);
        Assert.Equal(0.5, metrics.FalseActivationRate);
        Assert.Equal(TimeSpan.FromMilliseconds(100), metrics.AverageDetectionLatency);
    }

    [Fact]
    public void AssessmentRequiresEnoughTrialsAndQuality()
    {
        var results = Enumerable.Range(0, 5)
            .Select(index => new CalibrationTrialResult($"p{index}", true, true, TimeSpan.FromMilliseconds(100)))
            .Concat(Enumerable.Range(0, 3).Select(index =>
                new CalibrationTrialResult($"n{index}", false, false, TimeSpan.FromMilliseconds(500))))
            .ToArray();

        var assessment = CalibrationMetricsCalculator.Assess(results, new CalibrationThresholds());

        Assert.True(assessment.Passed);
        Assert.Empty(assessment.Reasons);
    }

    [Fact]
    public async Task SessionRequiresConsent()
    {
        var session = CreateSession(new QueueEvaluator([true]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RunNextTrialAsync(Audio(), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task SessionStoresMetadataReceiptWithoutAudio()
    {
        var plan = new CalibrationPlan("Hey Computer",
        [
            new("p", "Say it", true, CalibrationDistance.Near, CalibrationNoise.Quiet),
            new("n", "Do not say it", false, CalibrationDistance.Near, CalibrationNoise.Quiet)
        ]);
        var thresholds = new CalibrationThresholds(1, 1, 1, 0);
        var store = new InMemoryCalibrationStateStore();
        var session = new CalibrationSession(plan, thresholds, new QueueEvaluator([true, false]), store);
        await session.AcceptConsentAsync();
        await session.RunNextTrialAsync(Audio(), TimeSpan.FromSeconds(1));
        await session.RunNextTrialAsync(Audio(), TimeSpan.FromSeconds(1));

        var receipt = await session.CompleteAsync("microphone-1", DateTimeOffset.UnixEpoch);
        var saved = await store.LoadAsync();

        Assert.True(receipt.Passed);
        Assert.False(receipt.AudioRetained);
        Assert.Equal(receipt, saved.LatestCalibration);
    }

    [Fact]
    public async Task RerunClearsReceiptButKeepsConsent()
    {
        var plan = new CalibrationPlan("Hey Computer",
        [
            new("p", "Say it", true, CalibrationDistance.Near, CalibrationNoise.Quiet),
            new("n", "Do not say it", false, CalibrationDistance.Near, CalibrationNoise.Quiet)
        ]);
        var store = new InMemoryCalibrationStateStore();
        var session = new CalibrationSession(
            plan,
            new CalibrationThresholds(1, 1, 1, 0),
            new QueueEvaluator([true, false]),
            store);
        await session.AcceptConsentAsync();
        await session.RunNextTrialAsync(Audio(), TimeSpan.FromSeconds(1));
        await session.RunNextTrialAsync(Audio(), TimeSpan.FromSeconds(1));
        await session.CompleteAsync("microphone-1", DateTimeOffset.UtcNow);

        await session.ResetForRerunAsync();
        var state = await store.LoadAsync();

        Assert.True(state.ConsentAccepted);
        Assert.Null(state.LatestCalibration);
        Assert.Empty(session.Results);
    }

    [Fact]
    public void ViewModelAdvancesAndResets()
    {
        var plan = new CalibrationPlan("Hey Computer",
        [
            new("one", "First", true, CalibrationDistance.Near, CalibrationNoise.Quiet),
            new("two", "Second", false, CalibrationDistance.Near, CalibrationNoise.Quiet)
        ]);
        var model = new CalibrationViewModel(plan);

        Assert.Equal("First", model.Prompt);
        model.RecordCompletedTrial();
        Assert.Equal(0.5, model.Progress);
        Assert.Equal("Second", model.Prompt);
        model.RecordCompletedTrial();
        Assert.True(model.IsComplete);
        model.Reset();
        Assert.False(model.IsComplete);
    }

    [Theory]
    [InlineData("Hey Computer")]
    [InlineData("stop")]
    [InlineData("mute")]
    public void EasterEggRejectsDefaultAndOrdinaryCommands(string phrase)
    {
        var service = new EasterEggTriggerService(new InMemoryCalibrationStateStore());

        var result = service.Validate(phrase);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task EasterEggPersistsNormalizedMetadataWithoutAudio()
    {
        var store = new InMemoryCalibrationStateStore();
        var service = new EasterEggTriggerService(store);

        var metadata = await service.ConfigureAsync("  Engage, Party Mode! ", DateTimeOffset.UnixEpoch);
        var state = await store.LoadAsync();

        Assert.Equal("engage party mode", metadata.NormalizedPhrase);
        Assert.False(metadata.AudioRetained);
        Assert.Equal(metadata, state.EasterEggTrigger);
        Assert.True(service.Match("Engage party mode!", metadata).Matched);
        Assert.False(service.Match("engage normal mode", metadata).Matched);
    }

    [Fact]
    public async Task EasterEggCanBeDisabledEditedAndReset()
    {
        var store = new InMemoryCalibrationStateStore();
        var service = new EasterEggTriggerService(store);
        await service.ConfigureAsync("engage party mode", DateTimeOffset.UnixEpoch);

        await service.SetEnabledAsync(false);
        var disabled = (await store.LoadAsync()).EasterEggTrigger;
        Assert.False(service.Match("engage party mode", disabled).Matched);

        await service.ConfigureAsync("activate breakfast protocol", DateTimeOffset.UnixEpoch.AddDays(1));
        var edited = (await store.LoadAsync()).EasterEggTrigger;
        Assert.Equal("activate breakfast protocol", edited?.NormalizedPhrase);

        await service.SkipOrResetAsync();
        Assert.Null((await store.LoadAsync()).EasterEggTrigger);
    }

    private static CalibrationSession CreateSession(ICalibrationTrialEvaluator evaluator) => new(
        new CalibrationPlan("Hey Computer",
        [
            new("only", "Say it", true, CalibrationDistance.Near, CalibrationNoise.Quiet)
        ]),
        new CalibrationThresholds(1, 1),
        evaluator,
        new InMemoryCalibrationStateStore());

    private static async IAsyncEnumerable<AudioFrame> Audio()
    {
        yield return new AudioFrame(new float[160], 16_000, 1);
        await Task.Yield();
    }

    private sealed class QueueEvaluator(IEnumerable<bool> outcomes) : ICalibrationTrialEvaluator
    {
        private readonly Queue<bool> _outcomes = new(outcomes);

        public Task<(bool Detected, TimeSpan Elapsed)> EvaluateAsync(
            IAsyncEnumerable<AudioFrame> audio,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((_outcomes.Dequeue(), TimeSpan.FromMilliseconds(100)));
    }
}
