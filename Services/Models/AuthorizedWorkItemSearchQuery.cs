using FlowDesk.Models;

namespace FlowDesk.Services.Models;

public sealed record AuthorizedWorkItemSearchQuery
{
    public string? SearchText { get; init; }

    public string? Department { get; init; }

    public RequestPriority? Priority { get; init; }

    public WorkflowStatus? WorkflowStatus { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
