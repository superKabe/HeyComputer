namespace HeyComputer.Core.Credentials;

public interface ICredentialStore
{
    Task SetAsync(string key, string secret, CancellationToken cancellationToken = default);

    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
