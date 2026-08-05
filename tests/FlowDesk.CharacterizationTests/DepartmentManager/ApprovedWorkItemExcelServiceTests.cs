using ClosedXML.Excel;
using FlowDesk.Options;
using FlowDesk.Services;
using FlowDesk.Services.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowDesk.CharacterizationTests.DepartmentManager;

public sealed class ApprovedWorkItemExcelServiceTests
{
    [Fact]
    public async Task AppendAsync_WritesEightColumnsInRequiredOrder()
    {
        string directory = CreateTemporaryDirectory();
        string filePath = Path.Combine(directory, "approved.xlsx");
        try
        {
            ApprovedWorkItemExcelService service = CreateService(filePath);
            var result = await service.AppendAsync(CreateRow("REQ-100"));

            Assert.True(result.IsSuccess);
            using var workbook = new XLWorkbook(filePath);
            IXLWorksheet worksheet = workbook.Worksheet("Onaylanan Talepler");
            Assert.Equal(
                new[]
                {
                    "Analist",
                    "Yazılımcı",
                    "Sürüm Tarihi",
                    "Banksoft Teslim Tarihi",
                    "Beklenen Statü",
                    "Mevcut Statü",
                    "Talep No",
                    "Talep"
                },
                worksheet.Row(1).Cells(1, 8)
                    .Select(cell => cell.GetString())
                    .ToArray());
            Assert.Equal("Analist Adı", worksheet.Cell(2, 1).GetString());
            Assert.Equal("Yazılımcı Adı", worksheet.Cell(2, 2).GetString());
            Assert.Equal(new DateTime(2026, 9, 1),
                worksheet.Cell(2, 3).GetDateTime());
            Assert.Equal(new DateTime(2026, 9, 5),
                worksheet.Cell(2, 4).GetDateTime());
            Assert.Equal("Canlıya hazır", worksheet.Cell(2, 5).GetString());
            Assert.Equal("Onaylandı", worksheet.Cell(2, 6).GetString());
            Assert.Equal("REQ-100", worksheet.Cell(2, 7).GetString());
            Assert.Equal("Düzenlenmiş talep", worksheet.Cell(2, 8).GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AppendAsync_DuplicateRequestNumber_UpdatesExistingRow()
    {
        string directory = CreateTemporaryDirectory();
        string filePath = Path.Combine(directory, "approved.xlsx");
        try
        {
            ApprovedWorkItemExcelService service = CreateService(filePath);
            Assert.True((await service.AppendAsync(
                CreateRow(" REQ-200 "))).IsSuccess);
            var updatedRow = new ApprovedWorkItemExcelRow
            {
                AnalystDisplayName = "Yeni Analist",
                DeveloperDisplayName = "Yeni Yazılımcı",
                ReleaseDate = new DateTime(2026, 10, 1),
                BanksoftDeliveryDate = new DateTime(2026, 10, 5),
                ExpectedStatus = "Yeni beklenen statü",
                CurrentStatus = "Yeni mevcut statü",
                RequestNumber = "req-200",
                RequestDescription = "Güncel talep"
            };
            Assert.True((await service.AppendAsync(
                updatedRow)).IsSuccess);

            using var workbook = new XLWorkbook(filePath);
            IXLWorksheet worksheet = workbook.Worksheet("Onaylanan Talepler");
            Assert.Equal(2,
                worksheet.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
            Assert.Equal("Yeni Analist", worksheet.Cell(2, 1).GetString());
            Assert.Equal("Yeni Yazılımcı", worksheet.Cell(2, 2).GetString());
            Assert.Equal(new DateTime(2026, 10, 1),
                worksheet.Cell(2, 3).GetDateTime());
            Assert.Equal(new DateTime(2026, 10, 5),
                worksheet.Cell(2, 4).GetDateTime());
            Assert.Equal("Yeni beklenen statü",
                worksheet.Cell(2, 5).GetString());
            Assert.Equal("Yeni mevcut statü",
                worksheet.Cell(2, 6).GetString());
            Assert.Equal("REQ-200", worksheet.Cell(2, 7).GetString());
            Assert.Equal("Güncel talep", worksheet.Cell(2, 8).GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AppendAsync_ConcurrentDuplicateCalls_WriteOnce()
    {
        string directory = CreateTemporaryDirectory();
        string filePath = Path.Combine(directory, "approved.xlsx");
        try
        {
            ApprovedWorkItemExcelService service = CreateService(filePath);
            var results = await Task.WhenAll(
                Enumerable.Range(0, 8)
                    .Select(_ => service.AppendAsync(
                        CreateRow("REQ-300"))));

            Assert.All(results, result => Assert.True(result.IsSuccess));
            using var workbook = new XLWorkbook(filePath);
            IXLWorksheet worksheet = workbook.Worksheet("Onaylanan Talepler");
            Assert.Equal(2,
                worksheet.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ApprovedWorkItemExcelService CreateService(
        string filePath)
    {
        return new ApprovedWorkItemExcelService(
            Microsoft.Extensions.Options.Options.Create(
                new ApprovedWorkItemExcelOptions
                {
                    FilePath = filePath,
                    WorksheetName = "Onaylanan Talepler"
                }),
            new TestHostEnvironment
            {
                ContentRootPath = Path.GetDirectoryName(filePath)!
            },
            NullLogger<ApprovedWorkItemExcelService>.Instance);
    }

    private static ApprovedWorkItemExcelRow CreateRow(string requestNumber)
    {
        return new ApprovedWorkItemExcelRow
        {
            AnalystDisplayName = "Analist Adı",
            DeveloperDisplayName = "Yazılımcı Adı",
            ReleaseDate = new DateTime(2026, 9, 1),
            BanksoftDeliveryDate = new DateTime(2026, 9, 5),
            ExpectedStatus = "Canlıya hazır",
            CurrentStatus = "Onaylandı",
            RequestNumber = requestNumber,
            RequestDescription = "Düzenlenmiş talep"
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"flowdesk-excel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "FlowDesk.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
