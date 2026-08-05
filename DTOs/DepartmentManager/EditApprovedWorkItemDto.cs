using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FlowDesk.DTOs.DepartmentManager;

public sealed class EditApprovedWorkItemDto
{
    public int WorkItemId { get; set; }

    [BindNever]
    public int? AnalystId { get; set; }

    [BindNever]
    public string? RequestNumber { get; set; }

    public int? DeveloperId { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public DateTime? BanksoftDeliveryDate { get; set; }

    public string? ExpectedStatus { get; set; }

    public string? CurrentStatus { get; set; }

    public string? RequestDescription { get; set; }

    public string? RowVersion { get; set; }
}
