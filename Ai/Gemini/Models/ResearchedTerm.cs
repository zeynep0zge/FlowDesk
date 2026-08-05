namespace FlowDesk.Ai.Models;

public sealed class ResearchedTerm
{
    public required string Term { get; init; }

    public bool IsResolved { get; init; }

    public string? ExpandedForm { get; init; }

    public string? TurkishMeaning { get; init; }

    public string? Explanation { get; init; }
}