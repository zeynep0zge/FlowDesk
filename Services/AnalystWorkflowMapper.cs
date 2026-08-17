using FlowDesk.Models;
using FlowDesk.ViewModels.Analyst;

namespace FlowDesk.Services;

public static class AnalystWorkflowMapper
{
    public static AnalystInboxItemViewModel ToInboxItem(WorkItem workItem)
    {
        return new AnalystInboxItemViewModel
        {
            Id = workItem.Id,
            RequestNumber = workItem.RequestNumber,
            RequestDescription = workItem.RequestDescription,
            Department = workItem.Department,
            Priority = workItem.Priority,
            WorkflowStatus = workItem.WorkflowStatus,
            CurrentStatus = workItem.CurrentStatus,
            ManagerNote = workItem.ManagerNote,
            CreatedAt = workItem.CreatedAt,
            UpdatedAt = workItem.UpdatedAt
        };
    }

    public static AnalystReviewViewModel ToReview(WorkItem workItem)
    {
        return new AnalystReviewViewModel
        {
            Id = workItem.Id,
            RequestNumber = workItem.RequestNumber,
            RequestDescription = workItem.RequestDescription,
            Department = workItem.Department,
            Priority = workItem.Priority,
            WorkflowStatus = workItem.WorkflowStatus,
            CreatedAt = workItem.CreatedAt,
            AnalystId = workItem.AnalystId,
            DeveloperId = workItem.DeveloperId,
            ReleaseDate = workItem.ReleaseDate,
            BanksoftDeliveryDate = workItem.BanksoftDeliveryDate,
            ExpectedStatus = workItem.ExpectedStatus,
            CurrentStatus = workItem.CurrentStatus,
            AnalystNote = workItem.AnalystNote,
            ManagerNote = workItem.ManagerNote,
            FeedbackMessages = workItem.FeedbackMessages
                .OrderBy(message => message.CreatedAt)
                .ToList()
        };
    }
}
