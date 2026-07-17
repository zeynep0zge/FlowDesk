using FlowDesk.Models;

namespace FlowDesk.ViewModels.Analyst
{
    public class AnalystInboxViewModel
    {
        public List<AnalystInboxItemViewModel> WorkItems { get; set; }
            = new();

        public int ReturnedCount { get; set; }

        public int TotalCount => WorkItems.Count;

        public int NewRequestCount => WorkItems.Count(x =>
            x.WorkflowStatus == WorkflowStatus.Submitted);

        public int ReviewingCount => WorkItems.Count(x =>
            x.WorkflowStatus == WorkflowStatus.UnderAnalystReview);
    }

    public class AnalystInboxItemViewModel
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; } = string.Empty;

        public string RequestDescription { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public RequestPriority Priority { get; set; }

        public WorkflowStatus WorkflowStatus { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string? ManagerNote { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}