using System.Net;
using HeyComputer.Providers.Xai.Audio;
using HeyComputer.Providers.Xai.Credentials;

namespace HeyComputer.Providers.Xai.Tests;

public sealed class XaiTranscriptionProviderTests
{
    [Fact]
    public async Task UploadsWaveAndParsesTranscript()
    {
        byte[]? requestBytes = null;
        string? mediaType = null;
        var handler = new StubHttpHandler(async (request, _) =>
        {
            requestBytes = await request.Content!.ReadAsByteArrayAsync();
            mediaType = request.Content.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"hey computer\",\"language\":\"en\",\"duration\":0.01}")
            };
        });
        using var client = new HttpClient(handler);
        var provider = new XaiTranscriptionProvider(client, new StaticXaiApiKeyProvider("key"));

        var result = await provider.TranscribeAsync(TestStreams.Values(TestStreams.Frame()));

        Assert.Equal("hey computer", result.Text);
        Assert.Equal("en", result.Language);
        Assert.Equal("multipart/form-data", mediaType);
        Assert.NotNull(requestBytes);
        Assert.Contains("RIFF", System.Text.Encoding.ASCII.GetString(requestBytes!), StringComparison.Ordinal);
        Assert.Contains("utterance.wav", System.Text.Encoding.ASCII.GetString(requestBytes!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsAudioThatWasNotNormalized()
    {
        var calls = 0;
        var handler = new StubHttpHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = new HttpClient(handler);
        var provider = new XaiTranscriptionProvider(client, new StaticXaiApiKeyProvider("key"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.TranscribeAsync(TestStreams.Values(TestStreams.Frame(sampleRate: 48_000))));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task HonorsCancellationBeforeNetworkRequest()
    {
        var calls = 0;
        var handler = new StubHttpHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = new HttpClient(handler);
        var provider = new XaiTranscriptionProvider(client, new StaticXaiApiKeyProvider("key"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.TranscribeAsync(TestStreams.Values(TestStreams.Frame()), cancellation.Token));
        Assert.Equal(0, calls);
    }
}
