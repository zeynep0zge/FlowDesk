namespace FlowDesk.DTOs.DepartmentManager
{
    public class ApproveRequestDto
    {
        public int WorkItemId { get; set; }

        public int? DeveloperId { get; set; }

        public string? ManagerNote { get; set; }
    }
}
