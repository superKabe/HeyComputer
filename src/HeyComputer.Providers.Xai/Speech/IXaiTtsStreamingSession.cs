using System.Runtime.CompilerServices;

namespace HeyComputer.Providers.Xai.Speech;

public sealed record XaiTtsEvent(string Type, string? AudioBase64 = null);

public interface IXaiTtsStreamingSession : IAsyncDisposable
{
    Task ConnectAsync(Uri uri, string apiKey, CancellationToken cancellationToken);

    ValueTask SendTextAsync(string text, CancellationToken cancellationToken);

    ValueTask CompleteTextAsync(CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<XaiTtsEvent> ReceiveAsync(CancellationToken cancellationToken);
}

public interface IXaiTtsStreamingSessionFactory
{
    IXaiTtsStreamingSession Create();
}
