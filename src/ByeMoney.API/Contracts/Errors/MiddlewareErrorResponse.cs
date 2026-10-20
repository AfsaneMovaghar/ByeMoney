using System.Text.Json.Serialization;

namespace ByeMoney.API.Contracts.Errors;

public sealed record MiddlewareErrorResponse(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("errors")] IReadOnlyList<object> Errors);
