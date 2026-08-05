namespace FlowDesk.Options;

public sealed class ApprovedWorkItemExcelOptions
{
    public const string SectionName = "ApprovedWorkItemExcel";

    public string FilePath { get; init; } =
        "App_Data/approved-work-items.xlsx";

    public string WorksheetName { get; init; } = "Onaylanan Talepler";
}
