namespace HeyComputer.Core.Providers;

public sealed record ProviderManifest(
    string Id,
    string DisplayName,
    Uri? Endpoint,
    string? Model,
    string? CredentialReference,
    IReadOnlyDictionary<string, string>? Settings = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("A provider ID is required.", nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("A provider display name is required.", nameof(DisplayName));
        }

        if (Endpoint is not null)
        {
            if (!Endpoint.IsAbsoluteUri || !string.IsNullOrEmpty(Endpoint.UserInfo))
            {
                throw new ArgumentException("A provider endpoint must be an absolute URI without embedded credentials.", nameof(Endpoint));
            }

            var isSecureRemote = Endpoint.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            var isLocalHttp = Endpoint.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && Endpoint.IsLoopback;
            if (!isSecureRemote && !isLocalHttp)
            {
                throw new ArgumentException("Remote provider endpoints must use HTTPS; HTTP is allowed only for loopback endpoints.", nameof(Endpoint));
            }
        }

        ValidateReference(CredentialReference, nameof(CredentialReference), 256);

        if (Settings is not null)
        {
            foreach (var key in Settings.Keys)
            {
                ValidateReference(key, nameof(Settings), 128, required: true);
            }
        }
    }

    private static void ValidateReference(string? value, string parameterName, int maximumLength, bool required = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new ArgumentException("Provider setting keys cannot be empty.", parameterName);
            }

            return;
        }

        if (value.Length > maximumLength || value.Any(character =>
                char.IsControl(character) ||
                !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ':' or '/')))
        {
            throw new ArgumentException("Provider references and setting keys contain unsupported characters.", parameterName);
        }
    }
}
