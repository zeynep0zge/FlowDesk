namespace FlowDesk.Services.Models;

public sealed class ApprovedWorkItemListItemResult
{
    public int Id { get; init; }
    public int? DeveloperId { get; init; }
    public string Department { get; init; } = string.Empty;
    public string AnalystFullName { get; init; } = string.Empty;
    public string? AnalystBusinessCode { get; init; }
    public string DeveloperFullName { get; init; } = string.Empty;
    public string? DeveloperBusinessCode { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public DateTime? BanksoftDeliveryDate { get; init; }
    public string? ExpectedStatus { get; init; }
    public string CurrentStatus { get; init; } = string.Empty;
    public string RequestNumber { get; init; } = string.Empty;
    public string RequestDescription { get; init; } = string.Empty;
    public byte[]? RowVersion { get; init; }
}
