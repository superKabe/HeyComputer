using System.Net;

namespace HeyComputer.Providers.Xai;

public sealed class XaiProviderException : Exception
{
    public XaiProviderException(string operation, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(CreateMessage(operation, statusCode), innerException)
    {
        Operation = operation;
        StatusCode = statusCode;
    }

    public string Operation { get; }

    public HttpStatusCode? StatusCode { get; }

    private static string CreateMessage(string operation, HttpStatusCode? statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"xAI rejected the credential while {operation}.",
        HttpStatusCode.TooManyRequests => $"xAI rate-limited the request while {operation}.",
        >= HttpStatusCode.InternalServerError => $"xAI was temporarily unavailable while {operation}.",
        null => $"The xAI connection failed while {operation}.",
        _ => $"xAI could not complete the request while {operation}."
    };
}
