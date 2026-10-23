namespace HeyComputer.Providers.Xai.Tests;

public sealed class XaiProviderOptionsTests
{
    [Fact]
    public void RejectsInsecureRemoteEndpoint()
    {
        var options = new XaiProviderOptions(BaseUri: new Uri("http://api.example.test/v1/"));
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void WithExpressionRecomputesEffectiveEndpoint()
    {
        var options = new XaiProviderOptions() with { BaseUri = new Uri("https://gateway.example.test/xai") };
        Assert.Equal("https://gateway.example.test/xai/", options.EffectiveBaseUri.AbsoluteUri);
    }
}
