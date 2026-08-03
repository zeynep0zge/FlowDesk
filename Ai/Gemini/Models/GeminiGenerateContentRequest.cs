using System.Text.Json.Serialization;

namespace FlowDesk.Ai.Gemini.Models;

public sealed class GeminiGenerateContentRequest
{
    [JsonPropertyName("systemInstruction")]
    public required GeminiRequestContent SystemInstruction { get; init; }

    [JsonPropertyName("contents")]
    public IReadOnlyList<GeminiRequestContent> Contents { get; init; }
        = Array.Empty<GeminiRequestContent>();

    [JsonPropertyName("generationConfig")]
    public required GeminiGenerationConfig GenerationConfig { get; init; }
}

public sealed class GeminiRequestContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; init; }

    [JsonPropertyName("parts")]
    public IReadOnlyList<GeminiRequestPart> Parts { get; init; }
        = Array.Empty<GeminiRequestPart>();
}

public sealed class GeminiRequestPart
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

public sealed class GeminiGenerationConfig
{
    [JsonPropertyName("responseFormat")]
    public required GeminiResponseFormat ResponseFormat { get; init; }
}

public sealed class GeminiResponseFormat
{
    [JsonPropertyName("text")]
    public required GeminiTextResponseFormat Text { get; init; }
}

public sealed class GeminiTextResponseFormat
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = "APPLICATION_JSON";

    [JsonPropertyName("schema")]
    public required object Schema { get; init; }
}
