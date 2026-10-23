namespace HeyComputer.ComputerControl.Tests;

public sealed class LaunchAllowlistTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:1234")]
    [InlineData("spotify:")]
    public void AcceptsExplicitSafeUriSchemes(string uri)
    {
        var allowlist = new LaunchAllowlist(
        [
            new LaunchTarget("safe", "Safe target", LaunchTargetKind.Uri, uri)
        ]);

        Assert.True(allowlist.TryGet("safe", out _));
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-uri")]
    public void RejectsUnsafeOrInvalidUriSchemes(string uri)
    {
        Assert.Throws<ArgumentException>(() => new LaunchAllowlist(
        [
            new LaunchTarget("unsafe", "Unsafe target", LaunchTargetKind.Uri, uri)
        ]));
    }

    [Fact]
    public void RejectsDuplicateIdsIgnoringCase()
    {
        Assert.Throws<ArgumentException>(() => new LaunchAllowlist(
        [
            new LaunchTarget("spotify", "Spotify", LaunchTargetKind.Uri, "spotify:"),
            new LaunchTarget("SPOTIFY", "Spotify again", LaunchTargetKind.Uri, "spotify:")
        ]));
    }
}
