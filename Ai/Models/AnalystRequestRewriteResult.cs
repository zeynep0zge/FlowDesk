namespace FlowDesk.Ai.Models;

public sealed class AnalystRequestRewriteResult
{
    public required string RewrittenRequest { get; init; }

    public IReadOnlyList<AbbreviationExplanation> Abbreviations { get; init; }
        = Array.Empty<AbbreviationExplanation>();

    public IReadOnlyList<string> Ambiguities { get; init; }
        = Array.Empty<string>();

    public IReadOnlyList<string> UnresolvedTerms { get; init; }
        = Array.Empty<string>();
}