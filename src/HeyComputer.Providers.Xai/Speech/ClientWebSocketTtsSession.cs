using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace HeyComputer.Providers.Xai.Speech;

public sealed class ClientWebSocketTtsSessionFactory : IXaiTtsStreamingSessionFactory
{
    public IXaiTtsStreamingSession Create() => new ClientWebSocketTtsSession();
}

internal sealed class ClientWebSocketTtsSession : IXaiTtsStreamingSession
{
    private readonly ClientWebSocket _socket = new();

    public async Task ConnectAsync(Uri uri, string apiKey, CancellationToken cancellationToken)
    {
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        try
        {
            await _socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is WebSocketException or HttpRequestException)
        {
            throw new XaiProviderException("connecting to streaming speech", innerException: exception);
        }
    }

    public ValueTask SendTextAsync(string text, CancellationToken cancellationToken) =>
        SendJsonAsync(new { type = "text.delta", delta = text }, cancellationToken);

    public ValueTask CompleteTextAsync(CancellationToken cancellationToken) =>
        SendJsonAsync(new { type = "text.done" }, cancellationToken);

    public ValueTask ClearAsync(CancellationToken cancellationToken) =>
        SendJsonAsync(new { type = "text.clear" }, cancellationToken);

    public async IAsyncEnumerable<XaiTtsEvent> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var receiveBuffer = new byte[16 * 1024];
        while (_socket.State == WebSocketState.Open)
        {
            var message = new ArrayBufferWriter<byte>();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(receiveBuffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    yield break;
                }

                if (result.MessageType != WebSocketMessageType.Text || message.WrittenCount + result.Count > 2_000_000)
                {
                    throw new XaiProviderException("reading streaming speech");
                }

                message.Write(receiveBuffer.AsSpan(0, result.Count));
            }
            while (!result.EndOfMessage);

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(message.WrittenMemory);
            }
            catch (JsonException exception)
            {
                throw new XaiProviderException("reading streaming speech", innerException: exception);
            }

            using (document)
            {
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
                if (string.IsNullOrWhiteSpace(type))
                {
                    continue;
                }

                var audio = root.TryGetProperty("delta", out var deltaNode) && deltaNode.ValueKind == JsonValueKind.String
                    ? deltaNode.GetString()
                    : null;
                yield return new XaiTtsEvent(type, audio);
            }
        }
    }

    private async ValueTask SendJsonAsync(object payload, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("The xAI speech stream is not connected.");
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "completed", timeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
            {
                // Disposal is best effort and must not obscure the original operation result.
            }
        }

        _socket.Dispose();
    }
}
