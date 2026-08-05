using ClosedXML.Excel;
using FlowDesk.Common;
using FlowDesk.Options;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using Microsoft.Extensions.Options;

namespace FlowDesk.Services;

public sealed class ApprovedWorkItemExcelService
    : IApprovedWorkItemExcelService
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private static readonly string[] Headers =
    [
        "Analist",
        "Yazılımcı",
        "Sürüm Tarihi",
        "Banksoft Teslim Tarihi",
        "Beklenen Statü",
        "Mevcut Statü",
        "Talep No",
        "Talep"
    ];

    private readonly ApprovedWorkItemExcelOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ApprovedWorkItemExcelService> _logger;

    public ApprovedWorkItemExcelService(
        IOptions<ApprovedWorkItemExcelOptions> options,
        IHostEnvironment environment,
        ILogger<ApprovedWorkItemExcelService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ServiceResult> AppendAsync(
        ApprovedWorkItemExcelRow row,
        CancellationToken cancellationToken = default)
    {
        if (row == null)
        {
            return ServiceResult.Failure(
                "Excel kaydı için veri bulunamadı.");
        }

        string requestNumber = row.RequestNumber?.Trim() ?? string.Empty;
        if (requestNumber.Length == 0)
        {
            return ServiceResult.Failure(
                "Excel kaydı için talep numarası zorunludur.");
        }

        bool lockAcquired = false;
        try
        {
            await WriteLock.WaitAsync(cancellationToken);
            lockAcquired = true;

            string filePath = ResolveFilePath();
            string? directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using XLWorkbook workbook = File.Exists(filePath)
                ? new XLWorkbook(filePath)
                : new XLWorkbook();
            IXLWorksheet worksheet = GetOrCreateWorksheet(
                workbook,
                out bool worksheetCreated);

            bool formatHeaders = worksheetCreated ||
                IsHeaderRowEmpty(worksheet);
            EnsureHeaders(worksheet, formatHeaders);
            ApplyWorksheetFormatting(worksheet);

            int? existingRow = FindRequestRow(worksheet, requestNumber);
            int rowNumber = existingRow ?? FindFirstEmptyRow(worksheet);
            string storedRequestNumber = existingRow.HasValue
                ? worksheet.Cell(rowNumber, 7).GetString().Trim()
                : requestNumber;
            WriteRow(worksheet, rowNumber, row, storedRequestNumber);
            workbook.SaveAs(filePath);

            return ServiceResult.Success();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Onaylanan talep Excel kaydı iptal edildi.");
            return ServiceResult.Failure(
                "Excel kaydı tamamlanamadı.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Onaylanan talep Excel kaydı tamamlanamadı. Hata türü: {ErrorType}",
                exception.GetType().Name);
            return ServiceResult.Failure(
                "Excel kaydı tamamlanamadı.");
        }
        finally
        {
            if (lockAcquired)
            {
                WriteLock.Release();
            }
        }
    }

    public async Task<ServiceResult<ApprovedWorkItemExcelFile>> DownloadAsync(
        CancellationToken cancellationToken = default)
    {
        bool lockAcquired = false;
        try
        {
            await WriteLock.WaitAsync(cancellationToken);
            lockAcquired = true;

            string filePath = ResolveFilePath();
            if (!File.Exists(filePath))
            {
                return ServiceResult<ApprovedWorkItemExcelFile>.NotFound(
                    "Ortak Excel dosyası henüz oluşturulmamış.");
            }

            byte[] content = await File.ReadAllBytesAsync(
                filePath,
                cancellationToken);
            return ServiceResult<ApprovedWorkItemExcelFile>.Success(
                new ApprovedWorkItemExcelFile
                {
                    Content = content,
                    FileName = Path.GetFileName(filePath)
                });
        }
        catch (OperationCanceledException)
        {
            return ServiceResult<ApprovedWorkItemExcelFile>.Failure(
                "Ortak Excel indirme işlemi tamamlanamadı.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Ortak Excel okunamadı. Hata türü: {ErrorType}",
                exception.GetType().Name);
            return ServiceResult<ApprovedWorkItemExcelFile>.Failure(
                "Ortak Excel indirme işlemi tamamlanamadı.");
        }
        finally
        {
            if (lockAcquired)
            {
                WriteLock.Release();
            }
        }
    }

    private string ResolveFilePath()
    {
        if (string.IsNullOrWhiteSpace(_options.FilePath))
        {
            throw new InvalidOperationException(
                "Excel dosya yolu yapılandırılmamış.");
        }

        string configuredPath = _options.FilePath.Trim();
        return Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(
                    _environment.ContentRootPath,
                    configuredPath));
    }

    private IXLWorksheet GetOrCreateWorksheet(
        XLWorkbook workbook,
        out bool worksheetCreated)
    {
        string worksheetName = _options.WorksheetName?.Trim() ?? string.Empty;
        if (worksheetName.Length == 0)
        {
            throw new InvalidOperationException(
                "Excel çalışma sayfası adı yapılandırılmamış.");
        }

        if (workbook.TryGetWorksheet(
                worksheetName,
                out IXLWorksheet? worksheet))
        {
            worksheetCreated = false;
            return worksheet;
        }

        worksheetCreated = true;
        return workbook.Worksheets.Add(worksheetName);
    }

    private static bool IsHeaderRowEmpty(IXLWorksheet worksheet)
    {
        return Enumerable.Range(1, Headers.Length)
            .All(column => worksheet.Cell(1, column).IsEmpty());
    }

    private static void EnsureHeaders(
        IXLWorksheet worksheet,
        bool applyFormatting)
    {
        for (int column = 1; column <= Headers.Length; column++)
        {
            worksheet.Cell(1, column).Value = Headers[column - 1];
        }

        if (applyFormatting)
        {
            IXLRange headerRange = worksheet.Range(
                1,
                1,
                1,
                Headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            worksheet.SheetView.FreezeRows(1);
        }
    }

    private static void ApplyWorksheetFormatting(IXLWorksheet worksheet)
    {
        worksheet.Column(1).Width = 22;
        worksheet.Column(2).Width = 22;
        worksheet.Column(3).Width = 15;
        worksheet.Column(4).Width = 24;
        worksheet.Column(5).Width = 22;
        worksheet.Column(6).Width = 24;
        worksheet.Column(7).Width = 20;
        worksheet.Column(8).Width = 70;

        worksheet.Column(3).Style.DateFormat.Format = "dd.MM.yyyy";
        worksheet.Column(4).Style.DateFormat.Format = "dd.MM.yyyy";
        worksheet.Column(8).Style.Alignment.WrapText = true;
    }

    private static int? FindRequestRow(
        IXLWorksheet worksheet,
        string requestNumber)
    {
        int lastRow = worksheet.LastRowUsed(XLCellsUsedOptions.Contents)
            ?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            if (string.Equals(
                    worksheet.Cell(row, 7).GetString().Trim(),
                    requestNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    private static int FindFirstEmptyRow(IXLWorksheet worksheet)
    {
        int lastRow = worksheet.LastRowUsed(XLCellsUsedOptions.Contents)
            ?.RowNumber() ?? 1;
        for (int row = 2; row <= lastRow; row++)
        {
            bool empty = Enumerable.Range(1, Headers.Length)
                .All(column => worksheet.Cell(row, column).IsEmpty());
            if (empty)
            {
                return row;
            }
        }

        return Math.Max(2, lastRow + 1);
    }

    private static void WriteRow(
        IXLWorksheet worksheet,
        int rowNumber,
        ApprovedWorkItemExcelRow row,
        string requestNumber)
    {
        worksheet.Cell(rowNumber, 1).Value =
            NormalizeText(row.AnalystDisplayName);
        worksheet.Cell(rowNumber, 2).Value =
            NormalizeText(row.DeveloperDisplayName);
        SetDate(worksheet.Cell(rowNumber, 3), row.ReleaseDate);
        SetDate(worksheet.Cell(rowNumber, 4), row.BanksoftDeliveryDate);
        worksheet.Cell(rowNumber, 5).Value =
            NormalizeText(row.ExpectedStatus);
        worksheet.Cell(rowNumber, 6).Value =
            NormalizeText(row.CurrentStatus);
        worksheet.Cell(rowNumber, 7).Value = requestNumber;
        worksheet.Cell(rowNumber, 8).Value =
            NormalizeText(row.RequestDescription);
    }

    private static void SetDate(IXLCell cell, DateTime? value)
    {
        if (!value.HasValue)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = value.Value;
        cell.Style.DateFormat.Format = "dd.MM.yyyy";
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

}
