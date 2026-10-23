using HeyComputer.Core.Credentials;
using HeyComputer.Providers.Xai.Credentials;

namespace HeyComputer.Desktop.Integration;

public sealed class CredentialStoreXaiApiKeyProvider(
    ICredentialStore credentialStore,
    string credentialKey = "xai-api-key") : IXaiApiKeyProvider
{
    public const string CredentialKey = "xai-api-key";

    public async ValueTask<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var secret = await credentialStore.GetAsync(credentialKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("An xAI API key has not been configured.");
        }

        return secret;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default) =>
        !string.IsNullOrWhiteSpace(
            await credentialStore.GetAsync(credentialKey, cancellationToken).ConfigureAwait(false));
}
