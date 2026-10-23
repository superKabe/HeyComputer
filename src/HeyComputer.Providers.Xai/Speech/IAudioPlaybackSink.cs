namespace HeyComputer.Providers.Xai.Speech;

public sealed record StreamingAudioFormat(string Codec, int SampleRate, int Channels, int BitsPerSample);

public interface IAudioPlaybackSink
{
    ValueTask PlayAsync(
        ReadOnlyMemory<byte> audio,
        StreamingAudioFormat format,
        CancellationToken cancellationToken = default);
}
