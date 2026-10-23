using HeyComputer.Audio;
using NAudio.Wave;

namespace HeyComputer.Desktop.Audio;

public static class WaveFormatAdapter
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");

    public static RawAudioFormat ToRawAudioFormat(WaveFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var encoding = ResolveEncoding(format);
        return new RawAudioFormat(format.SampleRate, format.Channels, encoding);
    }

    private static PcmSampleEncoding ResolveEncoding(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            return PcmSampleEncoding.Float32;
        }

        if (format is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == FloatSubFormat && format.BitsPerSample == 32)
            {
                return PcmSampleEncoding.Float32;
            }

            if (extensible.SubFormat != PcmSubFormat)
            {
                throw new NotSupportedException("The selected microphone uses an unsupported sample encoding.");
            }
        }
        else if (format.Encoding != WaveFormatEncoding.Pcm)
        {
            throw new NotSupportedException("The selected microphone uses an unsupported sample encoding.");
        }

        return format.BitsPerSample switch
        {
            16 => PcmSampleEncoding.Signed16,
            24 => PcmSampleEncoding.Signed24,
            32 => PcmSampleEncoding.Signed32,
            _ => throw new NotSupportedException(
                $"The selected microphone's {format.BitsPerSample}-bit format is not supported.")
        };
    }
}
