using FlowDesk.Models;

namespace FlowDesk.Constants;

public static class AnalystFeedbackStatusOptions
{
    public static IReadOnlyList<KeyValuePair<WorkflowStatus, string>> All
        { get; } =
    [
        new(WorkflowStatus.UnderAnalystReview, "İncelemede"),
        new(WorkflowStatus.WaitingManagerApproval,
            "Yöneticiye gönderildi"),
        new(WorkflowStatus.Approved, "Onaylandı"),
        new(WorkflowStatus.Rejected, "Reddedildi"),
        new(WorkflowStatus.ReturnedToAnalyst, "Ek bilgi gerekli"),
        new(WorkflowStatus.ReturnedToBusinessUnit, "İade Et")
    ];

    public static bool Contains(WorkflowStatus status)
    {
        return All.Any(option => option.Key == status);
    }

    public static string GetLabel(WorkflowStatus status)
    {
        if (status == WorkflowStatus.ReturnedToBusinessUnit)
        {
            return WorkflowStatusDescriptions.GetDescription(status);
        }

        foreach (var option in All)
        {
            if (option.Key == status)
            {
                return option.Value;
            }
        }

        return status.ToString();
    }
}
