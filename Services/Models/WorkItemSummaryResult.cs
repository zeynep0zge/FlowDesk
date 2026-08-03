using FlowDesk.Models;

namespace FlowDesk.Services.Models;

public sealed record WorkItemSummaryResult(
    int Id,
    string RequestNumber,
    string RequestDescription,
    string Department,
    RequestPriority Priority,
    WorkflowStatus WorkflowStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
