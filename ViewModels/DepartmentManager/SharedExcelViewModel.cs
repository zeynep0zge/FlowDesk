using FlowDesk.ViewModels.Analyst;

namespace FlowDesk.ViewModels.DepartmentManager;

public sealed class SharedExcelViewModel
{
    public IReadOnlyList<SharedExcelItemViewModel> Requests { get; init; } = [];
}

public sealed class SharedExcelItemViewModel
{
    public const int RequestPreviewMaximumLength = 180;

    public int Id { get; init; }
    public int? DeveloperId { get; init; }
    public string AnalystDisplayName { get; init; } = string.Empty;
    public string DeveloperDisplayName { get; init; } = string.Empty;
    public IReadOnlyList<UserSelectionOptionViewModel> DeveloperOptions
        { get; init; } = [];
    public DateTime? ReleaseDate { get; init; }
    public DateTime? BanksoftDeliveryDate { get; init; }
    public string? ExpectedStatus { get; init; }
    public string CurrentStatus { get; init; } = string.Empty;
    public string RequestNumber { get; init; } = string.Empty;
    public string RequestDescription { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;

    public bool IsRequestTruncated =>
        RequestDescription.Length > RequestPreviewMaximumLength;

    public string RequestPreview => IsRequestTruncated
        ? RequestDescription[..(RequestPreviewMaximumLength - 1)]
            .TrimEnd() + "…"
        : RequestDescription;
}
