using System.Buffers.Binary;
using HeyComputer.Core.Audio;

namespace HeyComputer.Audio;

public static class WasapiAudioNormalizer
{
    public const int OutputSampleRate = 16_000;

    public static AudioFrame Normalize(
        ReadOnlySpan<byte> input,
        RawAudioFormat format,
        DateTimeOffset? capturedAt = null)
    {
        ArgumentNullException.ThrowIfNull(format);
        format.Validate();

        var bytesPerInterleavedFrame = checked(format.BytesPerSample * format.Channels);
        if (input.IsEmpty || input.Length % bytesPerInterleavedFrame != 0)
        {
            throw new ArgumentException("Audio bytes must contain complete interleaved sample frames.", nameof(input));
        }

        var sourceFrames = input.Length / bytesPerInterleavedFrame;
        var mono = new float[sourceFrames];
        for (var frame = 0; frame < sourceFrames; frame++)
        {
            double sum = 0;
            var frameOffset = frame * bytesPerInterleavedFrame;
            for (var channel = 0; channel < format.Channels; channel++)
            {
                var offset = frameOffset + (channel * format.BytesPerSample);
                sum += ReadSample(input.Slice(offset, format.BytesPerSample), format.Encoding);
            }

            mono[frame] = Math.Clamp((float)(sum / format.Channels), -1f, 1f);
        }

        var normalized = format.SampleRate == OutputSampleRate
            ? mono
            : ResampleLinear(mono, format.SampleRate, OutputSampleRate);

        return new AudioFrame(normalized, OutputSampleRate, 1, capturedAt ?? DateTimeOffset.UtcNow);
    }

    private static float ReadSample(ReadOnlySpan<byte> bytes, PcmSampleEncoding encoding) => encoding switch
    {
        PcmSampleEncoding.Signed16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,
        PcmSampleEncoding.Signed24 => ReadInt24LittleEndian(bytes) / 8_388_608f,
        PcmSampleEncoding.Signed32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2_147_483_648f,
        PcmSampleEncoding.Float32 => ReadFloat32(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    private static int ReadInt24LittleEndian(ReadOnlySpan<byte> bytes)
    {
        var value = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        return (value & 0x0080_0000) == 0 ? value : value | unchecked((int)0xFF00_0000);
    }

    private static float ReadFloat32(ReadOnlySpan<byte> bytes)
    {
        var value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes));
        return float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
    }

    private static float[] ResampleLinear(float[] source, int sourceRate, int targetRate)
    {
        if (source.Length == 1)
        {
            return [source[0]];
        }

        var targetLength = Math.Max(1, (int)Math.Round(source.Length * (double)targetRate / sourceRate));
        var result = new float[targetLength];
        var scale = (double)sourceRate / targetRate;

        for (var index = 0; index < targetLength; index++)
        {
            var position = Math.Min(index * scale, source.Length - 1d);
            var left = (int)position;
            var right = Math.Min(left + 1, source.Length - 1);
            var fraction = (float)(position - left);
            result[index] = source[left] + ((source[right] - source[left]) * fraction);
        }

        return result;
    }
}
