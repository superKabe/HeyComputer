using System.Collections.Concurrent;

namespace HeyComputer.Core.Credentials;

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        _secrets[key] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(key);
        _secrets.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(key);
        return Task.FromResult(_secrets.TryRemove(key, out _));
    }

    private static void ValidateKey(string key) => ArgumentException.ThrowIfNullOrWhiteSpace(key);
}
