using System.Threading.Channels;
using HeyComputer.Core.Audio;

namespace HeyComputer.Audio;

public sealed class BoundedAudioFrameChannel
{
    private readonly Channel<AudioFrame> _channel;

    public BoundedAudioFrameChannel(int capacity = 64)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _channel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public bool TryWrite(AudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return _channel.Writer.TryWrite(frame);
    }

    public IAsyncEnumerable<AudioFrame> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public bool TryComplete(Exception? error = null) => _channel.Writer.TryComplete(error);
}
