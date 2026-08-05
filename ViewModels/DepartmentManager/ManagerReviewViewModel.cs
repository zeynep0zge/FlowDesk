using FlowDesk.Models;

namespace FlowDesk.ViewModels.DepartmentManager
{
    public class ManagerReviewViewModel
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; } = string.Empty;

        public string RequestDescription { get; set; } = string.Empty;

        public string? AiEditedRequest { get; set; }

        public string? Department { get; set; }

        public RequestPriority Priority { get; set; }

        public WorkflowStatus WorkflowStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? AnalystId { get; set; }

        public string AnalystDisplayName { get; set; } = string.Empty;

        public int? DeveloperId { get; set; }

        public string DeveloperDisplayName { get; set; } = string.Empty;

        public List<FlowDesk.ViewModels.Analyst.UserSelectionOptionViewModel>
            DeveloperOptions { get; set; } = [];

        public DateTime? ReleaseDate { get; set; }

        public DateTime? BanksoftDeliveryDate { get; set; }

        public string? ExpectedStatus { get; set; }

        public string? CurrentStatus { get; set; }

        public string? AnalystNote { get; set; }

        public string? ManagerNote { get; set; }
    }
}
