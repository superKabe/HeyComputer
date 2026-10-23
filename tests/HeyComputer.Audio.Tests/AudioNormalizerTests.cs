using System.Buffers.Binary;

namespace HeyComputer.Audio.Tests;

public sealed class AudioNormalizerTests
{
    [Fact]
    public void Pcm16Stereo48Khz_DownmixesAndResamples()
    {
        var bytes = new byte[480 * 2 * 2];
        for (var frame = 0; frame < 480; frame++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(frame * 4, 2), 16_384);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan((frame * 4) + 2, 2), -8_192);
        }

        var result = WasapiAudioNormalizer.Normalize(bytes, new RawAudioFormat(48_000, 2, PcmSampleEncoding.Signed16));

        Assert.Equal(16_000, result.SampleRate);
        Assert.Equal(1, result.Channels);
        Assert.Equal(160, result.Samples.Length);
        Assert.All(result.Samples.ToArray(), sample => Assert.InRange(sample, 0.124f, 0.126f));
    }

    [Theory]
    [InlineData(PcmSampleEncoding.Signed24, 3)]
    [InlineData(PcmSampleEncoding.Signed32, 4)]
    [InlineData(PcmSampleEncoding.Float32, 4)]
    public void Supported32And24BitFormats_AreNormalized(PcmSampleEncoding encoding, int byteCount)
    {
        var bytes = new byte[byteCount];
        switch (encoding)
        {
            case PcmSampleEncoding.Signed24:
                bytes[0] = 0;
                bytes[1] = 0;
                bytes[2] = 0x40;
                break;
            case PcmSampleEncoding.Signed32:
                BinaryPrimitives.WriteInt32LittleEndian(bytes, 1_073_741_824);
                break;
            case PcmSampleEncoding.Float32:
                BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(0.5f));
                break;
        }

        var result = WasapiAudioNormalizer.Normalize(bytes, new RawAudioFormat(16_000, 1, encoding));
        Assert.InRange(result.Samples.Span[0], 0.499f, 0.501f);
    }

    [Fact]
    public void IncompleteInterleavedFrame_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => WasapiAudioNormalizer.Normalize(
            [0, 1, 2],
            new RawAudioFormat(16_000, 2, PcmSampleEncoding.Signed16)));
    }

    [Fact]
    public void NonFiniteFloat_IsSilenced()
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(float.NaN));
        var result = WasapiAudioNormalizer.Normalize(bytes, new RawAudioFormat(16_000, 1, PcmSampleEncoding.Float32));
        Assert.Equal(0f, result.Samples.Span[0]);
    }
}
