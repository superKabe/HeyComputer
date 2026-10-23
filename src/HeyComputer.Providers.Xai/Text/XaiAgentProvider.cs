using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeyComputer.Core.Providers;
using HeyComputer.Providers.Xai.Credentials;
using HeyComputer.Providers.Xai.Http;

namespace HeyComputer.Providers.Xai.Text;

public sealed class XaiAgentProvider : IAgentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IXaiApiKeyProvider _apiKeyProvider;
    private readonly XaiProviderOptions _options;

    public XaiAgentProvider(HttpClient httpClient, IXaiApiKeyProvider apiKeyProvider, XaiProviderOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
        _options = options ?? new XaiProviderOptions();
        _options.Validate();
    }

    public string Id => "xai-responses";

    public ProviderCapabilities Capabilities { get; } = new(false, true, true, SupportsTools: false);

    public async IAsyncEnumerable<string> StreamResponseAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoint = new Uri(_options.EffectiveBaseUri, "responses");
        using var httpRequest = await XaiHttpRequest.CreateAsync(
            HttpMethod.Post, endpoint, _apiKeyProvider, cancellationToken).ConfigureAwait(false);
        httpRequest.Content = JsonContent.Create(CreatePayload(request), options: JsonOptions);

        using var response = await XaiHttpRequest.SendAsync(
            _httpClient,
            httpRequest,
            "generating a response",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data.Length == 0 || data == "[DONE]")
            {
                if (data == "[DONE]")
                {
                    yield break;
                }

                continue;
            }

            if (data.Length > 1_000_000)
            {
                throw new XaiProviderException("reading a response stream");
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(data);
            }
            catch (JsonException exception)
            {
                throw new XaiProviderException("reading a response stream", innerException: exception);
            }

            using (document)
            {
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
                if (type == "response.output_text.delta" &&
                    root.TryGetProperty("delta", out var deltaNode) &&
                    deltaNode.ValueKind == JsonValueKind.String)
                {
                    var delta = deltaNode.GetString();
                    if (!string.IsNullOrEmpty(delta))
                    {
                        yield return delta;
                    }
                }
                else if (type == "error")
                {
                    throw new XaiProviderException("generating a response");
                }
            }
        }
    }

    private object CreatePayload(AgentRequest request)
    {
        var input = new List<object>
        {
            new { role = "system", content = request.Instructions }
        };

        foreach (var message in request.History)
        {
            var role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
            input.Add(new { role, content = message.Content });
        }

        input.Add(new { role = "user", content = request.UserText });
        return new
        {
            model = _options.TextModel,
            input,
            stream = true,
            store = false,
            include = new[] { "no_inline_citations" }
        };
    }
}
