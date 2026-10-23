using NAudio.Wave;

namespace HeyComputer.Desktop.Audio;

public sealed class AudioFrameEventArgs(byte[] buffer, WaveFormat format) : EventArgs
{
    public ReadOnlyMemory<byte> Buffer { get; } = buffer;
    public WaveFormat Format { get; } = format;
}
