using FlowDesk.Models;

namespace FlowDesk.ViewModels.DepartmentManager
{
    public class ManagerInboxItemViewModel
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }
            = string.Empty;

        public string RequestDescription { get; set; }
            = string.Empty;

        public string Department { get; set; }
            = string.Empty;

        public RequestPriority Priority { get; set; }

        public WorkflowStatus WorkflowStatus { get; set; }

        public string CurrentStatus { get; set; }
            = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}