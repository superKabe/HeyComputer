using HeyComputer.Core.Providers;

namespace HeyComputer.Core.Tests;

public sealed class ProviderManifestTests
{
    [Fact]
    public void Validate_AllowsHttpsAndLoopbackHttp()
    {
        Create(new Uri("https://api.example.com/v1")).Validate();
        Create(new Uri("http://localhost:11434/v1")).Validate();
        Create(new Uri("http://127.0.0.1:11434/v1")).Validate();
    }

    [Fact]
    public void Validate_RejectsInsecureRemoteEndpointAndEmbeddedCredentials()
    {
        Assert.Throws<ArgumentException>(() => Create(new Uri("http://api.example.com/v1")).Validate());
        Assert.Throws<ArgumentException>(() => Create(new Uri("https://user:secret@api.example.com/v1")).Validate());
    }

    [Fact]
    public void Validate_RejectsUnsafeCredentialReferencesAndSettingKeys()
    {
        Assert.Throws<ArgumentException>(() =>
            (Create(new Uri("https://api.example.com")) with { CredentialReference = "secret\nvalue" }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Create(new Uri("https://api.example.com")) with
            {
                Settings = new Dictionary<string, string> { ["bad key"] = "value" }
            }).Validate());
    }

    private static ProviderManifest Create(Uri endpoint) =>
        new("provider", "Provider", endpoint, "model", "heycomputer/provider");
}
