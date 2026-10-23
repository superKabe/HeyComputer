using System.Runtime.CompilerServices;
using HeyComputer.Providers.Xai.Credentials;
using HeyComputer.Providers.Xai.Speech;

namespace HeyComputer.Providers.Xai.Tests;

public sealed class XaiStreamingTextToSpeechProviderTests
{
    [Fact]
    public async Task StreamsTextAndPlaysDecodedAudio()
    {
        var session = new FakeTtsSession([
            new XaiTtsEvent("audio.delta", Convert.ToBase64String([1, 2, 3, 4])),
            new XaiTtsEvent("audio.done")
        ]);
        var sink = new RecordingAudioSink();
        var provider = new XaiStreamingTextToSpeechProvider(
            new StaticXaiApiKeyProvider("key"),
            new FakeTtsSessionFactory(session),
            sink);

        await provider.SpeakAsync(TestStreams.Values("Hello", " there"), "eve");

        Assert.Equal(["Hello", " there"], session.Text);
        Assert.True(session.Completed);
        Assert.Equal([1, 2, 3, 4], sink.Bytes);
        Assert.Equal(24_000, sink.Format?.SampleRate);
        Assert.Contains("voice=eve", session.Uri?.Query, StringComparison.Ordinal);
        Assert.Equal("key", session.ApiKey);
    }

    [Fact]
    public async Task CancellationClearsTheActiveUtterance()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new FakeTtsSession([], cancellation);
        var provider = new XaiStreamingTextToSpeechProvider(
            new StaticXaiApiKeyProvider("key"),
            new FakeTtsSessionFactory(session),
            new RecordingAudioSink());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.SpeakAsync(TestStreams.Values("Hello"), null, cancellation.Token));
        Assert.True(session.Cleared);
    }

    private sealed class FakeTtsSessionFactory(FakeTtsSession session) : IXaiTtsStreamingSessionFactory
    {
        public IXaiTtsStreamingSession Create() => session;
    }

    private sealed class FakeTtsSession(
        IReadOnlyList<XaiTtsEvent> events,
        CancellationTokenSource? cancelOnReceive = null) : IXaiTtsStreamingSession
    {
        public List<string> Text { get; } = [];
        public bool Completed { get; private set; }
        public bool Cleared { get; private set; }
        public Uri? Uri { get; private set; }
        public string? ApiKey { get; private set; }

        public Task ConnectAsync(Uri uri, string apiKey, CancellationToken cancellationToken)
        {
            Uri = uri;
            ApiKey = apiKey;
            return Task.CompletedTask;
        }

        public ValueTask SendTextAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Text.Add(text);
            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteTextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Completed = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken)
        {
            Cleared = true;
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<XaiTtsEvent> ReceiveAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (cancelOnReceive is not null)
            {
                cancelOnReceive.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            foreach (var item in events)
            {
                await Task.Yield();
                yield return item;
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingAudioSink : IAudioPlaybackSink
    {
        public List<byte> Bytes { get; } = [];
        public StreamingAudioFormat? Format { get; private set; }

        public ValueTask PlayAsync(
            ReadOnlyMemory<byte> audio,
            StreamingAudioFormat format,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Bytes.AddRange(audio.ToArray());
            Format = format;
            return ValueTask.CompletedTask;
        }
    }
}
