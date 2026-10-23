namespace HeyComputer.Audio;

public sealed record RawAudioFormat(int SampleRate, int Channels, PcmSampleEncoding Encoding)
{
    public int BytesPerSample => Encoding switch
    {
        PcmSampleEncoding.Signed16 => 2,
        PcmSampleEncoding.Signed24 => 3,
        PcmSampleEncoding.Signed32 or PcmSampleEncoding.Float32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(Encoding))
    };

    public void Validate()
    {
        if (SampleRate is < 8_000 or > 384_000)
        {
            throw new ArgumentOutOfRangeException(nameof(SampleRate), "Sample rate must be between 8 kHz and 384 kHz.");
        }

        if (Channels is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(Channels), "Channel count must be between 1 and 32.");
        }
    }
}
