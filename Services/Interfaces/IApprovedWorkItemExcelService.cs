using FlowDesk.Common;
using FlowDesk.Services.Models;

namespace FlowDesk.Services.Interfaces;

public interface IApprovedWorkItemExcelService
{
    Task<ServiceResult> AppendAsync(
        ApprovedWorkItemExcelRow row,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ApprovedWorkItemExcelFile>> DownloadAsync(
        CancellationToken cancellationToken = default);
}
