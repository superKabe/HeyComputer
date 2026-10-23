using HeyComputer.Core.Credentials;

namespace HeyComputer.Core.Tests;

public sealed class CredentialStoreTests
{
    [Fact]
    public async Task InMemoryStore_SupportsSetReplaceReadAndDelete()
    {
        var store = new InMemoryCredentialStore();

        await store.SetAsync("xai", "first");
        await store.SetAsync("xai", "second");

        Assert.Equal("second", await store.GetAsync("xai"));
        Assert.True(await store.DeleteAsync("xai"));
        Assert.Null(await store.GetAsync("xai"));
        Assert.False(await store.DeleteAsync("xai"));
    }

    [Fact]
    public async Task InMemoryStore_HonorsCancellation()
    {
        var store = new InMemoryCredentialStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.SetAsync("xai", "secret", cancellation.Token));
    }
}
