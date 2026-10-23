using System.Net;
using System.Text;
using HeyComputer.Core.Personality;
using HeyComputer.Core.Providers;
using HeyComputer.Providers.Xai.Credentials;
using HeyComputer.Providers.Xai.Text;

namespace HeyComputer.Providers.Xai.Tests;

public sealed class XaiAgentProviderTests
{
    [Fact]
    public async Task StreamsResponseDeltasAndSendsPrivacySafePayload()
    {
        string? body = null;
        string? authorization = null;
        var handler = new StubHttpHandler(async (request, _) =>
        {
            authorization = request.Headers.Authorization?.ToString();
            body = await request.Content!.ReadAsStringAsync();
            var events = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Hello\"}\n\n" +
                         "data: {\"type\":\"response.output_text.delta\",\"delta\":\" there\"}\n\n" +
                         "data: [DONE]\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(events, Encoding.UTF8, "text/event-stream")
            };
        });
        using var client = new HttpClient(handler);
        var provider = new XaiAgentProvider(client, new StaticXaiApiKeyProvider("secret-key"));
        var request = new AgentRequest(
            "Hi",
            PersonalityProfile.Helpful,
            [new ConversationMessage("assistant", "Earlier")],
            "Be concise.");

        var chunks = new List<string>();
        await foreach (var chunk in provider.StreamResponseAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["Hello", " there"], chunks);
        Assert.Equal("Bearer secret-key", authorization);
        Assert.Contains("\"store\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"stream\":true", body, StringComparison.Ordinal);
        Assert.Contains("Be concise.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpFailureDoesNotExposeProviderBodyOrCredential()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("secret-key private prompt")
        }));
        using var client = new HttpClient(handler);
        var provider = new XaiAgentProvider(client, new StaticXaiApiKeyProvider("secret-key"));
        var request = new AgentRequest("private prompt", PersonalityProfile.Helpful, [], "instructions");

        var exception = await Assert.ThrowsAsync<XaiProviderException>(async () =>
        {
            await foreach (var _ in provider.StreamResponseAsync(request))
            {
            }
        });

        Assert.DoesNotContain("secret-key", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private prompt", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public void StaticCredentialNeverPrintsTheKey()
    {
        var credential = new StaticXaiApiKeyProvider("do-not-print");
        Assert.Equal(nameof(StaticXaiApiKeyProvider), credential.ToString());
    }
}
