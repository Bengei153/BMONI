using System.Net;

namespace BMoni.Infrastructure.Bmoni;

public sealed class BmoniApiException(HttpStatusCode statusCode, string operation) : Exception($"BMONI {operation} failed with HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
