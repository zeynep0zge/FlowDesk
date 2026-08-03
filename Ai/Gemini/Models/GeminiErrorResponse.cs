using System.Text.Json.Serialization;

namespace FlowDesk.Ai.Gemini.Models;

public sealed class GeminiErrorResponse
{
    [JsonPropertyName("error")]
    public GeminiErrorDetails? Error { get; init; }
}

public sealed class GeminiErrorDetails
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
