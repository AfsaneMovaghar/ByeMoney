using System.Net;

namespace ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;

public class TarhElahiUnavailableException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public TarhElahiUnavailableException(string message, Exception? innerException = null, HttpStatusCode? statusCode = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

