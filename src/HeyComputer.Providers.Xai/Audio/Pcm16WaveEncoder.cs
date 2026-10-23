using System.Buffers.Binary;
using HeyComputer.Core.Audio;

namespace HeyComputer.Providers.Xai.Audio;

internal static class Pcm16WaveEncoder
{
    private const int SampleRate = 16_000;
    private const int Channels = 1;
    private const int MaximumSamples = SampleRate * 60 * 5;

    public static async Task<byte[]> EncodeAsync(
        IAsyncEnumerable<AudioFrame> frames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frames);
        using var pcm = new MemoryStream();
        var sampleBuffer = new byte[2];
        var sampleCount = 0;

        await foreach (var frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (frame.SampleRate != SampleRate || frame.Channels != Channels)
            {
                throw new ArgumentException("xAI transcription requires normalized 16 kHz mono audio.", nameof(frames));
            }

            foreach (var sample in frame.Samples.Span)
            {
                if (++sampleCount > MaximumSamples)
                {
                    throw new ArgumentException("A transcription request cannot exceed five minutes.", nameof(frames));
                }

                var clamped = Math.Clamp(sample, -1f, 1f);
                var value = clamped <= -1f ? short.MinValue : (short)Math.Round(clamped * short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(sampleBuffer, value);
                pcm.Write(sampleBuffer);
            }
        }

        if (sampleCount == 0)
        {
            throw new ArgumentException("A transcription request requires audio.", nameof(frames));
        }

        var pcmBytes = pcm.ToArray();
        using var wave = new MemoryStream(44 + pcmBytes.Length);
        using var writer = new BinaryWriter(wave, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes.Length);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)Channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * Channels * 2);
        writer.Write((short)(Channels * 2));
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);
        writer.Flush();
        return wave.ToArray();
    }
}
