namespace FlowDesk.DTOs.DepartmentManager;

public sealed class RejectRequestDto
{
    public int WorkItemId { get; set; }

    public string? ManagerNote { get; set; }
}
