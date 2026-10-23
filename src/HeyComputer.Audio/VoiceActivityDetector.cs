using HeyComputer.Core.Audio;

namespace HeyComputer.Audio;

public sealed record VoiceActivityOptions(float RmsThreshold = 0.02f)
{
    public void Validate()
    {
        if (!float.IsFinite(RmsThreshold) || RmsThreshold is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(RmsThreshold));
        }
    }
}

public sealed class VoiceActivityDetector
{
    private readonly VoiceActivityOptions _options;

    public VoiceActivityDetector(VoiceActivityOptions? options = null)
    {
        _options = options ?? new VoiceActivityOptions();
        _options.Validate();
    }

    public bool IsSpeech(AudioFrame frame)
    {
        RollingAudioBuffer.ValidateFrame(frame);
        double sumSquares = 0;
        foreach (var sample in frame.Samples.Span)
        {
            sumSquares += sample * sample;
        }

        var rms = Math.Sqrt(sumSquares / frame.Samples.Length);
        return rms >= _options.RmsThreshold;
    }
}
