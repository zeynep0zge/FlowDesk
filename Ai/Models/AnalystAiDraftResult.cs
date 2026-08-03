namespace FlowDesk.Ai.Models;

public sealed class AnalystAiDraftResult
{
    public required string RewrittenRequest { get; init; }

    public IReadOnlyList<AbbreviationExplanation> Abbreviations { get; init; }
        = Array.Empty<AbbreviationExplanation>();

    public IReadOnlyList<string> Ambiguities { get; init; }
        = Array.Empty<string>();

    public IReadOnlyList<string> UnresolvedTerms { get; init; }
        = Array.Empty<string>();

    public required string RowVersion { get; init; }

    public DateTime GeneratedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
