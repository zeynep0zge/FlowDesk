using ClosedXML.Excel;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.Services
{
    public class ExcelExportService : IExcelExportService
    {
        public byte[] CreateApprovedRequestExcel(
            ManagerReviewViewModel request)
        {
            ArgumentNullException.ThrowIfNull(request);

            using XLWorkbook workbook = new();

            IXLWorksheet worksheet =
                workbook.Worksheets.Add("Onaylanan Talep");

            CreateTitle(worksheet);

            int row = 3;

            AddTextRow(
                worksheet,
                ref row,
                "Talep Numarası",
                request.RequestNumber);

            AddTextRow(
                worksheet,
                ref row,
                "Talep Açıklaması",
                request.RequestDescription);

            AddTextRow(
                worksheet,
                ref row,
                "Departman",
                request.Department);

            AddTextRow(
                worksheet,
                ref row,
                "Öncelik",
                GetPriorityText(request.Priority));

            AddTextRow(
                worksheet,
                ref row,
                "Workflow Durumu",
                GetWorkflowStatusText(request.WorkflowStatus));

            AddTextRow(
                worksheet,
                ref row,
                "Mevcut Statü",
                request.CurrentStatus);

            AddDateRow(
                worksheet,
                ref row,
                "Oluşturulma Tarihi",
                request.CreatedAt);

            AddTextRow(
                worksheet,
                ref row,
                "Analist ID",
                request.AnalystId?.ToString());

            AddTextRow(
                worksheet,
                ref row,
                "Yazılımcı ID",
                request.DeveloperId?.ToString());

            AddDateRow(
                worksheet,
                ref row,
                "Sürüm Tarihi",
                request.ReleaseDate);

            AddDateRow(
                worksheet,
                ref row,
                "Banksoft Teslim Tarihi",
                request.BanksoftDeliveryDate);

            AddTextRow(
                worksheet,
                ref row,
                "Beklenen Statü",
                request.ExpectedStatus);

            AddTextRow(
                worksheet,
                ref row,
                "Analist Notu",
                request.AnalystNote);

            AddTextRow(
                worksheet,
                ref row,
                "Yönetici Notu",
                request.ManagerNote);

            FormatWorksheet(worksheet, row);

            using MemoryStream stream = new();

            workbook.SaveAs(stream);

            return stream.ToArray();
        }

        private static void CreateTitle(
            IXLWorksheet worksheet)
        {
            worksheet.Cell("A1").Value =
                "FlowDesk - Onaylanan Talep";

            worksheet.Range("A1:B1").Merge();

            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 16;

            worksheet.Cell("A1")
                .Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A1")
                .Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 28;
        }

        private static void AddTextRow(
            IXLWorksheet worksheet,
            ref int row,
            string label,
            string? value)
        {
            worksheet.Cell(row, 1).Value = label;

            worksheet.Cell(row, 2).Value =
                string.IsNullOrWhiteSpace(value)
                    ? "-"
                    : value.Trim();

            FormatLabelCell(worksheet.Cell(row, 1));

            worksheet.Cell(row, 2)
                .Style.Alignment.WrapText = true;

            row++;
        }

        private static void AddDateRow(
            IXLWorksheet worksheet,
            ref int row,
            string label,
            DateTime? value)
        {
            worksheet.Cell(row, 1).Value = label;

            if (value.HasValue)
            {
                worksheet.Cell(row, 2).Value = value.Value;

                worksheet.Cell(row, 2)
                    .Style.DateFormat.Format =
                        "dd.MM.yyyy";
            }
            else
            {
                worksheet.Cell(row, 2).Value = "-";
            }

            FormatLabelCell(worksheet.Cell(row, 1));

            row++;
        }

        private static void FormatLabelCell(
            IXLCell cell)
        {
            cell.Style.Font.Bold = true;

            cell.Style.Fill.BackgroundColor =
                XLColor.LightGray;
        }

        private static void FormatWorksheet(
            IXLWorksheet worksheet,
            int lastRow)
        {
            IXLRange range =
                worksheet.Range(3, 1, lastRow - 1, 2);

            range.Style.Border.TopBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.BottomBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.LeftBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.RightBorder =
                XLBorderStyleValues.Thin;

            range.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Top;

            worksheet.Column(1).Width = 28;
            worksheet.Column(2).Width = 60;

            worksheet.Column(2)
                .Style.Alignment.WrapText = true;

            worksheet.SheetView.FreezeRows(2);
        }

        private static string GetPriorityText(
            RequestPriority priority)
        {
            return priority switch
            {
                RequestPriority.Low => "Düşük",
                RequestPriority.Normal => "Normal",
                RequestPriority.High => "Yüksek",
                RequestPriority.Critical => "Kritik",
                _ => priority.ToString()
            };
        }

        private static string GetWorkflowStatusText(
            WorkflowStatus workflowStatus)
        {
            return WorkflowStatusDescriptions.GetDescription(
                workflowStatus);
        }
    }
}