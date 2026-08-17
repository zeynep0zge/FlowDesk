namespace FlowDesk.ViewModels.Shared;

public sealed class FeedbackNotificationViewModel
{
    public int UnreadCount { get; init; }

    public string TargetController { get; init; } = string.Empty;

    public string TargetAction { get; init; } = string.Empty;

    public List<FeedbackNotificationItemViewModel> Items { get; init; } = [];
}

public sealed class FeedbackNotificationItemViewModel
{
    public int WorkItemId { get; init; }

    public string RequestNumber { get; init; } = string.Empty;

    public string SenderName { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
