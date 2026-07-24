using FlowDesk.Common;
using FlowDesk.Models;

namespace FlowDesk.Services.Interfaces
{
    public interface IProjectManagerWorkItemService
    {
        Task<ServiceResult<List<WorkItem>>>
            GetMyRequestsAsync(int? currentUserId);

        Task<ServiceResult<WorkItem>> GetDetailsAsync(
            int id,
            int? currentUserId);

        Task<ServiceResult<string>>
            ValidateCreateAsync(string requestNumber);

        Task<ServiceResult> CreateAsync(
            WorkItem workItem,
            string normalizedRequestNumber,
            int? currentUserId);

        Task<ServiceResult<WorkItem>> GetForEditAsync(
            int id,
            int? currentUserId);

        Task<ServiceResult<string>> ValidateUpdateAsync(
            int id,
            string requestNumber);

        Task<ServiceResult> UpdateAsync(
            int id,
            WorkItem changes,
            string normalizedRequestNumber,
            int? currentUserId);

        Task<ServiceResult<WorkItem>> GetForDeleteAsync(
            int id,
            int? currentUserId);

        Task<ServiceResult> DeleteAsync(
            int id,
            int? currentUserId);
    }
}