namespace HeyComputer.Providers.Xai.Credentials;

public interface IXaiApiKeyProvider
{
    ValueTask<string> GetApiKeyAsync(CancellationToken cancellationToken = default);
}

public sealed class StaticXaiApiKeyProvider : IXaiApiKeyProvider
{
    private readonly string _apiKey;

    public StaticXaiApiKeyProvider(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Any(char.IsControl))
        {
            throw new ArgumentException("An xAI API key is required.", nameof(apiKey));
        }

        _apiKey = apiKey;
    }

    public ValueTask<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_apiKey);
    }

    public override string ToString() => nameof(StaticXaiApiKeyProvider);
}
