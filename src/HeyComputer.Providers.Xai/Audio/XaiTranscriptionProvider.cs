using System.Net.Http.Headers;
using System.Text.Json;
using HeyComputer.Core.Audio;
using HeyComputer.Core.Providers;
using HeyComputer.Providers.Xai.Credentials;
using HeyComputer.Providers.Xai.Http;

namespace HeyComputer.Providers.Xai.Audio;

public sealed class XaiTranscriptionProvider : ITranscriptionProvider
{
    private readonly HttpClient _httpClient;
    private readonly IXaiApiKeyProvider _apiKeyProvider;
    private readonly XaiProviderOptions _options;

    public XaiTranscriptionProvider(
        HttpClient httpClient,
        IXaiApiKeyProvider apiKeyProvider,
        XaiProviderOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
        _options = options ?? new XaiProviderOptions();
        _options.Validate();
    }

    public string Id => "xai-stt";

    public ProviderCapabilities Capabilities { get; } = new(false, false, true, [16_000]);

    public async Task<TranscriptionResult> TranscribeAsync(
        IAsyncEnumerable<AudioFrame> audio,
        CancellationToken cancellationToken = default)
    {
        var wave = await Pcm16WaveEncoder.EncodeAsync(audio, cancellationToken).ConfigureAwait(false);
        var endpoint = new Uri(_options.EffectiveBaseUri, "stt");
        using var request = await XaiHttpRequest.CreateAsync(
            HttpMethod.Post, endpoint, _apiKeyProvider, cancellationToken).ConfigureAwait(false);
        using var form = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(wave);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent, "file", "utterance.wav");
        form.Add(new StringContent(_options.TranscriptionModel), "model");
        form.Add(new StringContent(_options.Language), "language");
        form.Add(new StringContent("true"), "format");
        request.Content = form;

        using var response = await XaiHttpRequest.SendAsync(
            _httpClient,
            request,
            "transcribing speech",
            HttpCompletionOption.ResponseContentRead,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var text = root.TryGetProperty("text", out var textNode) ? textNode.GetString() : null;
            if (text is null)
            {
                throw new XaiProviderException("reading a transcription");
            }

            var language = root.TryGetProperty("language", out var languageNode) ? languageNode.GetString() : null;
            return new TranscriptionResult(text, language);
        }
        catch (JsonException exception)
        {
            throw new XaiProviderException("reading a transcription", innerException: exception);
        }
    }
}
