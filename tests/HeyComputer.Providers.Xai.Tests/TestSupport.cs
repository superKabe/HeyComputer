using System.Net;
using HeyComputer.Core.Audio;

namespace HeyComputer.Providers.Xai.Tests;

internal sealed class StubHttpHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => handler(request, cancellationToken);
}

internal static class TestStreams
{
    public static async IAsyncEnumerable<T> Values<T>(params T[] values)
    {
        foreach (var value in values)
        {
            await Task.Yield();
            yield return value;
        }
    }

    public static AudioFrame Frame(float value = 0.25f, int sampleRate = 16_000, int channels = 1) =>
        new(Enumerable.Repeat(value, 160).ToArray(), sampleRate, channels);
}
