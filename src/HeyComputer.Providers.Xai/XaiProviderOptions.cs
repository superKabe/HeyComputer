namespace HeyComputer.Providers.Xai;

public sealed record XaiProviderOptions(
    string TextModel = "grok-4.7",
    string TranscriptionModel = "grok-voice-transcribe-2.0",
    Uri? BaseUri = null,
    string Language = "en",
    string DefaultVoice = "eve")
{
    public Uri EffectiveBaseUri => ValidateBaseUri(BaseUri ?? new Uri("https://api.x.ai/v1/"));

    public void Validate()
    {
        _ = EffectiveBaseUri;
        RequireValue(TextModel, nameof(TextModel), 128);
        RequireValue(TranscriptionModel, nameof(TranscriptionModel), 128);
        RequireValue(Language, nameof(Language), 32);
        RequireValue(DefaultVoice, nameof(DefaultVoice), 128);
    }

    private static Uri ValidateBaseUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("The xAI base URI must be an absolute HTTPS URI without embedded credentials.", nameof(BaseUri));
        }

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");
    }

    private static void RequireValue(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.Any(char.IsControl))
        {
            throw new ArgumentException($"{name} must be a nonempty value no longer than {maximumLength} characters.", name);
        }
    }
}
