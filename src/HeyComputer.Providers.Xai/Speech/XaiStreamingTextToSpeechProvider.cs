using HeyComputer.Core.Providers;
using HeyComputer.Providers.Xai.Credentials;

namespace HeyComputer.Providers.Xai.Speech;

public sealed class XaiStreamingTextToSpeechProvider : ITextToSpeechProvider
{
    private static readonly StreamingAudioFormat OutputFormat = new("pcm", 24_000, 1, 16);
    private readonly IXaiApiKeyProvider _apiKeyProvider;
    private readonly IXaiTtsStreamingSessionFactory _sessionFactory;
    private readonly IAudioPlaybackSink _playbackSink;
    private readonly XaiProviderOptions _options;

    public XaiStreamingTextToSpeechProvider(
        IXaiApiKeyProvider apiKeyProvider,
        IXaiTtsStreamingSessionFactory sessionFactory,
        IAudioPlaybackSink playbackSink,
        XaiProviderOptions? options = null)
    {
        _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _playbackSink = playbackSink ?? throw new ArgumentNullException(nameof(playbackSink));
        _options = options ?? new XaiProviderOptions();
        _options.Validate();
    }

    public string Id => "xai-streaming-tts";

    public ProviderCapabilities Capabilities { get; } = new(
        false,
        true,
        true,
        Voices: ["eve", "ara"]);

    public async Task SpeakAsync(
        IAsyncEnumerable<string> text,
        string? voice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var selectedVoice = string.IsNullOrWhiteSpace(voice) ? _options.DefaultVoice : voice.Trim();
        if (selectedVoice.Length > 128 || selectedVoice.Any(char.IsControl))
        {
            throw new ArgumentException("The voice identifier is invalid.", nameof(voice));
        }

        var socketBase = new UriBuilder(_options.EffectiveBaseUri)
        {
            Scheme = Uri.UriSchemeWss,
            Path = CombinePath(_options.EffectiveBaseUri.AbsolutePath, "tts"),
            Query = $"language={Uri.EscapeDataString(_options.Language)}&voice={Uri.EscapeDataString(selectedVoice)}&codec=pcm&sample_rate=24000&optimize_streaming_latency=1"
        }.Uri;

        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        await using var session = _sessionFactory.Create();
        await session.ConnectAsync(socketBase, apiKey, cancellationToken).ConfigureAwait(false);

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sendTask = SendTextAsync(session, text, operation.Token);
        try
        {
            await foreach (var item in session.ReceiveAsync(operation.Token).WithCancellation(operation.Token).ConfigureAwait(false))
            {
                switch (item.Type)
                {
                    case "audio.delta" when !string.IsNullOrEmpty(item.AudioBase64):
                        byte[] bytes;
                        try
                        {
                            bytes = Convert.FromBase64String(item.AudioBase64);
                        }
                        catch (FormatException exception)
                        {
                            throw new XaiProviderException("decoding streaming speech", innerException: exception);
                        }

                        await _playbackSink.PlayAsync(bytes, OutputFormat, operation.Token).ConfigureAwait(false);
                        break;
                    case "audio.done":
                        await sendTask.ConfigureAwait(false);
                        return;
                    case "error":
                        throw new XaiProviderException("synthesizing speech");
                }
            }

            await sendTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                await session.ClearAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Cancellation remains the authoritative outcome.
            }

            throw;
        }
        finally
        {
            operation.Cancel();
            if (!sendTask.IsCompletedSuccessfully)
            {
                try
                {
                    await sendTask.ConfigureAwait(false);
                }
                catch (Exception) when (operation.IsCancellationRequested)
                {
                    // The receive side completed or failed first.
                }
            }
        }
    }

    private static async Task SendTextAsync(
        IXaiTtsStreamingSession session,
        IAsyncEnumerable<string> text,
        CancellationToken cancellationToken)
    {
        await foreach (var chunk in text.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            if (chunk.Length > 60_000)
            {
                throw new ArgumentException("An individual speech chunk cannot exceed 60,000 characters.", nameof(text));
            }

            await session.SendTextAsync(chunk, cancellationToken).ConfigureAwait(false);
        }

        await session.CompleteTextAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string CombinePath(string basePath, string segment) =>
        $"{basePath.TrimEnd('/')}/{segment}";
}
