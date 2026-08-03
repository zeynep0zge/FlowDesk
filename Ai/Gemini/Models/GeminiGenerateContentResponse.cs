using System.Text.Json.Serialization;

namespace FlowDesk.Ai.Gemini.Models;

public sealed class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public IReadOnlyList<GeminiCandidate> Candidates { get; init; }
        = Array.Empty<GeminiCandidate>();
}

public sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; }

    [JsonPropertyName("groundingMetadata")]
    public GeminiGroundingMetadata? GroundingMetadata { get; init; }
}

public sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public IReadOnlyList<GeminiPart> Parts { get; init; }
        = Array.Empty<GeminiPart>();
}

public sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed class GeminiGroundingMetadata
{
    [JsonPropertyName("groundingChunks")]
    public IReadOnlyList<GeminiGroundingChunk> GroundingChunks { get; init; }
        = Array.Empty<GeminiGroundingChunk>();
}

public sealed class GeminiGroundingChunk
{
    [JsonPropertyName("web")]
    public GeminiGroundingWebSource? Web { get; init; }
}

public sealed class GeminiGroundingWebSource
{
    [JsonPropertyName("uri")]
    public string? Uri { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }
}