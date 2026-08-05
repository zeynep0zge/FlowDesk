namespace FlowDesk.Services.Models;

public sealed class ApprovedWorkItemExcelFile
{
    public required byte[] Content { get; init; }

    public required string FileName { get; init; }
}
