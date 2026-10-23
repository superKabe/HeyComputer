using System.Net.Http.Headers;
using HeyComputer.Providers.Xai.Credentials;

namespace HeyComputer.Providers.Xai.Http;

internal static class XaiHttpRequest
{
    public static async Task<HttpRequestMessage> CreateAsync(
        HttpMethod method,
        Uri uri,
        IXaiApiKeyProvider apiKeyProvider,
        CancellationToken cancellationToken)
    {
        var apiKey = await apiKeyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Any(char.IsControl))
        {
            throw new InvalidOperationException("The configured xAI credential is unavailable.");
        }

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpRequestMessage request,
        string operation,
        HttpCompletionOption completion,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = response.StatusCode;
                response.Dispose();
                throw new XaiProviderException(operation, statusCode);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (XaiProviderException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new XaiProviderException(operation, innerException: exception);
        }
    }
}
