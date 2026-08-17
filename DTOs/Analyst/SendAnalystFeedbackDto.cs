using FlowDesk.Models;

namespace FlowDesk.DTOs.Analyst;

public sealed class SendAnalystFeedbackDto
{
    public int WorkItemId { get; set; }

    public WorkflowStatus? FeedbackStatus { get; set; }

    public string? Description { get; set; }
}
