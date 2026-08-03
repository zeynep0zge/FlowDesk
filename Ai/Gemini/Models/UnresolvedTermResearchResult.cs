namespace FlowDesk.Ai.Models;

public sealed class UnresolvedTermResearchResult
{
    public IReadOnlyList<ResearchedTerm> Terms { get; init; }
        = Array.Empty<ResearchedTerm>();

    public IReadOnlyList<GroundingSource> Sources { get; init; }
        = Array.Empty<GroundingSource>();
}