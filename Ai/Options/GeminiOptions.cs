namespace FlowDesk.Ai.Options;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public required string ApiKey { get; init; }

    public string Model { get; init; } = "gemini-3.6-flash";

    public string BaseUrl { get; init; }
        = "https://generativelanguage.googleapis.com/v1beta/";

    public int TimeoutSeconds { get; init; } = 30;
}