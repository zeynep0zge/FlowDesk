namespace FlowDesk.Services.Models;

public sealed class ApprovedWorkItemExcelRow
{
    public required string AnalystDisplayName { get; init; }
    public required string DeveloperDisplayName { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public DateTime? BanksoftDeliveryDate { get; init; }
    public string? ExpectedStatus { get; init; }
    public string? CurrentStatus { get; init; }
    public required string RequestNumber { get; init; }
    public required string RequestDescription { get; init; }
}
