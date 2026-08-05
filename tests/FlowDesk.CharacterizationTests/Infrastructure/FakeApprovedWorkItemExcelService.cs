using FlowDesk.Common;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public sealed class FakeApprovedWorkItemExcelService
    : IApprovedWorkItemExcelService
{
    private readonly object _sync = new();
    private readonly List<ApprovedWorkItemExcelRow> _rows = [];

    public bool ShouldFail { get; set; }

    public int CallCount { get; private set; }

    public int DownloadCallCount { get; private set; }

    public IReadOnlyList<ApprovedWorkItemExcelRow> Rows
    {
        get
        {
            lock (_sync)
            {
                return _rows.ToArray();
            }
        }
    }

    public Task<ServiceResult> AppendAsync(
        ApprovedWorkItemExcelRow row,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            CallCount++;
            if (!ShouldFail)
            {
                _rows.Add(row);
            }
        }

        return Task.FromResult(
            ShouldFail
                ? ServiceResult.Failure("Fake Excel failure.")
                : ServiceResult.Success());
    }

    public Task<ServiceResult<ApprovedWorkItemExcelFile>> DownloadAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            DownloadCallCount++;
        }

        return Task.FromResult(
            ServiceResult<ApprovedWorkItemExcelFile>.Success(
                new ApprovedWorkItemExcelFile
                {
                    Content = [1, 2, 3],
                    FileName = "approved-work-items.xlsx"
                }));
    }

    public void Clear()
    {
        lock (_sync)
        {
            _rows.Clear();
            CallCount = 0;
            DownloadCallCount = 0;
            ShouldFail = false;
        }
    }
}
