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

        Task<ServiceResult> CreateAsync(
            WorkItem workItem,
            int? currentUserId);

        Task<ServiceResult<WorkItem>> GetForEditAsync(
            int id,
            int? currentUserId);

        Task<ServiceResult> UpdateAsync(
            int id,
            WorkItem changes,
            int? currentUserId);

        Task<ServiceResult<WorkItem>> GetForDeleteAsync(
            int id,
            int? currentUserId);

        Task<ServiceResult> DeleteAsync(
            int id,
            int? currentUserId);
    }
}
