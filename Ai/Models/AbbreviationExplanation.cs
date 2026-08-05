namespace FlowDesk.Ai.Models;

public sealed class AbbreviationExplanation
{
    public required string Abbreviation { get; init; }

    public required string ExpandedForm { get; init; }

    public string? Explanation { get; init; }
}